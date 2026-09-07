using Application.DTOs.ChatMessage;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappers;

public class ChatProfile : Profile
{
    public ChatProfile()
    {
        CreateMap<ChatMessage, ChatMessageResponse>();
    }
}
