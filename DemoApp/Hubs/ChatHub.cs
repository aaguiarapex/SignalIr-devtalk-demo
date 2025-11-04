using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace DemoApp.Hubs;

public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, Participant> Participants = new();

    private static readonly IReadOnlyDictionary<string, string> AllowedRooms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet"] = "#.NET",
        ["java"] = "#Java",
        ["full-stack-html"] = "#Full Stack HTML"
    };

    public async Task JoinRoom(string room, string user)
    {
        var normalizedRoom = NormalizeRoom(room ?? string.Empty);
        var normalizedUser = NormalizeUser(user);

        if (string.IsNullOrEmpty(normalizedRoom) || string.IsNullOrEmpty(normalizedUser))
        {
            return;
        }

        if (Participants.TryGetValue(Context.ConnectionId, out var existing))
        {
            if (existing.Room == normalizedRoom && existing.User == normalizedUser)
            {
                await Clients.Caller.SendAsync(
                    "JoinedRoom",
                    existing.Room,
                    GetRoomLabel(existing.Room),
                    existing.User,
                    DateTimeOffset.UtcNow);
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, existing.Room);
            await Clients.Group(existing.Room).SendAsync(
                "ReceiveMessage",
                "sistema",
                $"{existing.User} salió de {GetRoomLabel(existing.Room)}",
                DateTimeOffset.UtcNow);
        }

        var participant = new Participant(normalizedRoom, normalizedUser);
        Participants[Context.ConnectionId] = participant;

        await Groups.AddToGroupAsync(Context.ConnectionId, participant.Room);
        await Clients.Group(participant.Room).SendAsync(
            "ReceiveMessage",
            "sistema",
            $"{participant.User} se unió a {GetRoomLabel(participant.Room)}",
            DateTimeOffset.UtcNow);

        await Clients.Caller.SendAsync(
            "JoinedRoom",
            participant.Room,
            GetRoomLabel(participant.Room),
            participant.User,
            DateTimeOffset.UtcNow);
    }

    public async Task SendMessage(string room, string user, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!Participants.TryGetValue(Context.ConnectionId, out var participant))
        {
            return;
        }

        var normalizedRoom = NormalizeRoom(room ?? string.Empty);

        if (!string.Equals(participant.Room, normalizedRoom, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await Clients.Group(participant.Room).SendAsync(
            "ReceiveMessage",
            participant.User,
            message.Trim(),
            DateTimeOffset.UtcNow);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Participants.TryRemove(Context.ConnectionId, out var participant))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, participant.Room);
            await Clients.Group(participant.Room).SendAsync(
                "ReceiveMessage",
                "sistema",
                $"{participant.User} salió de {GetRoomLabel(participant.Room)}",
                DateTimeOffset.UtcNow);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string NormalizeUser(string user)
    {
        return user.Trim();
    }

    private static string NormalizeRoom(string room)
    {
        var raw = room.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var slug = Regex.Replace(raw, "[^a-z0-9-]+", "-");
        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');

        return AllowedRooms.ContainsKey(slug) ? slug : string.Empty;
    }

    private record Participant(string Room, string User);

    private static string GetRoomLabel(string room)
    {
        return AllowedRooms.TryGetValue(room, out var label) ? label : $"#{room}";
    }
}
