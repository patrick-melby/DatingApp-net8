using System;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

public class MessageHub(IMessageRepository messageRepository, 
    IUnitOfWork unitOfWork, 
    IMapper mapper,
    IHubContext<PresenceHub> presenceHup): Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var otherUser = httpContext?.Request.Query["user"];
        if(Context.User is null || string.IsNullOrEmpty(otherUser)) throw new HubException("Cannot join group");
        var groupName = GetGroupName(Context.User.GetUsername(), otherUser);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        var group = await AddToGroup(groupName);

        await Clients.Group(groupName).SendAsync("UpdatedGroup", group);

        var messages = await messageRepository.GetMessageThread(Context.User.GetUsername(), otherUser!);

        if(unitOfWork.HasChanged()) await unitOfWork.Complete();

        await Clients.Caller.SendAsync("ReceiveMessageThread", messages);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var group = await RemoveFromMessageGroup();
        await Clients.Group(group.Name).SendAsync("UpdatedGroup", group);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(CreateMessageDto createMessageDto) 
    {
        var username = Context.User?.GetUsername() ?? throw new HubException("Could not get user");

        if(username == createMessageDto.RecipientUserName.ToLower()) throw new HubException("You cannot message yourself");

        var sender = await unitOfWork.UserRepository.GetUserByUsernameAsync(username);
        var recipient = await unitOfWork.UserRepository.GetUserByUsernameAsync(createMessageDto.RecipientUserName);

        if(sender is null || recipient is null || sender.UserName is null || recipient.UserName is null) 
            throw new HubException("Cannot send message at this time");

        var message = new Message()
        {
            Sender = sender,
            Recipient = recipient,
            SenderUsername = sender.UserName,
            RecipientUserName = recipient.UserName,
            Content = createMessageDto.Content
        };

        var groupName = GetGroupName(sender.UserName, recipient.UserName);
        var group = await messageRepository.GetMessageGroup(groupName);

        //recipient is online, so update read indicator
        if(group is not null && group.Connections.Any(x => x.Username == recipient.UserName))
        {
            message.DateRead = DateTime.UtcNow;
        }
        else 
        {
            var connections = await PresenceTracker.GetConnectionsForUser(recipient.UserName);
            if(connections is not null && connections?.Count != null)
            {
                await presenceHup.Clients.Clients(connections).SendAsync("NewMessageReceived", new {username = sender.UserName, knownAs = sender.KnownAs});
            }
        }

        messageRepository.AddMessage(message);

        if(await unitOfWork.Complete()) 
        {
            await Clients.Group(groupName).SendAsync("NewMessage", mapper.Map<MessageDto>(message));
        }
    }

    private async Task<Group> AddToGroup(string groupName)
    {
        var userName = Context.User?.GetUsername() ?? throw new Exception("Cannot get username");
        var group = await messageRepository.GetMessageGroup(groupName);
        var Connection = new Connection(){
            ConnectionId = Context.ConnectionId,
            Username = userName
        };

        if(group is null)
        {
            group = new Group(){
                Name = groupName
            };
            messageRepository.AddGroup(group);
        }

        group.Connections.Add(Connection);

       if(await unitOfWork.Complete()) return group;

       throw new HubException("Failed to join group");
    }

    private async Task<Group> RemoveFromMessageGroup()
    {
        var group = await messageRepository.GetGroupForConnection(Context.ConnectionId);
        var connection = group?.Connections.FirstOrDefault(X => X.ConnectionId == Context.ConnectionId);
        if(connection is not null && group is not null)
        {
            messageRepository.RemoveConnection(connection);
            if(await unitOfWork.Complete()) return group;
        }

        throw new Exception("Failed to remove from group");
    }

    private string GetGroupName(string caller, string? other)
    {
        var stringCompare = string.CompareOrdinal(caller, other) < 0;
        return stringCompare ? $"{caller}-{other}": $"{other}-{caller}";
    }
}
