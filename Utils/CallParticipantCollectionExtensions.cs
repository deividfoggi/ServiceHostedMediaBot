using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceHostedMediaBot.Utils
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Graph;
    using Microsoft.Graph.Communications.Calls;
    using Microsoft.Graph.Communications.Common;

    public static class CallParticipantCollectionExtensions
    {
        public static Task<IParticipant> WaitForParticipantAsync(
            this IParticipantCollection participants,
            Func<IParticipant, bool> match,
            string failureMessage = null,
            TimeSpan timeOut = default(TimeSpan))
        {
            failureMessage =
                failureMessage
                ?? $"Timed out while waiting for participant in collection {participants.ResourcePath}";

            return participants.WaitForUpdateAsync<IParticipantCollection, IParticipant, Participant>(
                args => args.AddedResources.FirstOrDefault(match),
                failureMessage,
                timeOut);
        }

        public static Task<IParticipant> WaitForParticipantAsync(
            this IParticipantCollection participants,
            string participantId,
            string failureMessage = null,
            TimeSpan timeOut = default(TimeSpan))
        {
            failureMessage =
                failureMessage
                ?? $"Timed out while waiting for participant {participantId} in collection {participants.ResourcePath}";

            return participants.WaitForParticipantAsync(
                participant => participantId.EqualsIgnoreCase(participant.Resource.Id),
                failureMessage,
                timeOut);
        }

        public static Task<IParticipant> WaitForRemovedParticipantAsync(
            this IParticipantCollection participants,
            Func<IParticipant, bool> match,
            string failureMessage = null,
            TimeSpan timeOut = default(TimeSpan))
        {
            failureMessage =
                failureMessage
                ?? $"Timed out while waiting for participant to be removed from collection {participants.ResourcePath}";

            return participants.WaitForUpdateAsync<IParticipantCollection, IParticipant, Participant>(
                args => args.RemovedResources.FirstOrDefault(match),
                failureMessage,
                timeOut);
        }
    }
}
