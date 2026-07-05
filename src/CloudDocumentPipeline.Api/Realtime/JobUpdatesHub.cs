using Microsoft.AspNetCore.SignalR;

namespace CloudDocumentPipeline.Api.Realtime;

// SignalR hub used by browsers to subscribe to job lifecycle updates.
// The server broadcasts a jobUpdated event whenever a worker publishes a status change.
public sealed class JobUpdatesHub : Hub
{
}
