namespace ServiceHostedMediaBot.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Graph;
    using Microsoft.Graph.Communications.Resources;

    public static class ResourceExtensions
    {
        public static async Task WaitForUpdateAsync<TResource, TEntity>(
            this TResource resource,
            Func<ResourceEventArgs<TEntity>, bool> match,
            string failureMessage = null,
            TimeSpan timeOut = default(TimeSpan))
            where TResource : IResource<TResource, TEntity>
            where TEntity : Entity
        {
            failureMessage =
                failureMessage
                ?? $"Timed out while waiting for update in {resource.ResourcePath}";

            if (timeOut == TimeSpan.Zero)
            {
                timeOut = TimeSpan.FromSeconds(60);
            }

            var matchedTcs = new TaskCompletionSource<bool>();

            void ResourceOnUpdated(TResource sender, ResourceEventArgs<TEntity> e)
            {
                if (match(e))
                {
                    matchedTcs.TrySetResult(true);
                }
            }

            resource.OnUpdated += ResourceOnUpdated;

            var eventArgs = new ResourceEventArgs<TEntity>(null, resource.Resource, resource.ResourcePath);

            try
            {
                if (match(eventArgs))
                {
                    return;
                }

                await matchedTcs.Task.ValidateAsync(
                    timeOut,
                    failureMessage).ConfigureAwait(false);
            }
            finally
            {
                resource.OnUpdated -= ResourceOnUpdated;
            }
        }

        public static async Task<TResource> WaitForUpdateAsync<TResourceCollection, TResource, TEntity>(
            this TResourceCollection resourceCollection,
            Func<CollectionEventArgs<TResource>, TResource> match,
            string failureMessage = null,
            TimeSpan timeOut = default(TimeSpan))
            where TResourceCollection : IResourceCollection<TResourceCollection, TResource, TEntity>
            where TResource : IResource<TResource, TEntity>
            where TEntity : Entity
        {
            failureMessage =
                failureMessage
                ?? $"Timed out while waiting for update in collection {resourceCollection.ResourcePath}";

            if (timeOut == TimeSpan.Zero)
            {
                timeOut = TimeSpan.FromSeconds(60);
            }

            var matchedTcs = new TaskCompletionSource<TResource>();
            void OnUpdated(TResourceCollection sender, CollectionEventArgs<TResource> e)
            {
                var resource = match(e);
                if (resource != null)
                {
                    matchedTcs.TrySetResult(resource);
                }
            }

            resourceCollection.OnUpdated += OnUpdated;

            var existingResources = new List<TResource>(resourceCollection);
            var collectionEventArgs = new CollectionEventArgs<TResource>(resourceCollection.ResourcePath, addedResources: existingResources);

            try
            {
                var resource = match(collectionEventArgs);
                if (resource != null)
                {
                    return resource;
                }

                return await matchedTcs.Task.ValidateAsync(
                    timeOut,
                    failureMessage).ConfigureAwait(false);
            }
            finally
            {
                resourceCollection.OnUpdated -= OnUpdated;
            }
        }
    }
}
