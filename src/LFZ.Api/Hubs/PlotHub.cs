using Microsoft.AspNetCore.SignalR;

namespace LFZ.Api.Hubs;

/// <summary>Broadcasts plot status changes so connected map clients refresh in real time.</summary>
public class PlotHub : Hub
{
}
