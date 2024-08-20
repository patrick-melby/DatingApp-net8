using System;

namespace API.DTOs;

public class CreateMessageDto
{
    public required string RecipientUserName { get; set; }
    public required string Content { get; set; }
}
