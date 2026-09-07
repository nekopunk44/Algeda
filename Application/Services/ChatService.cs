using Application.DTOs.ChatMessage;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;

namespace Application.Services;

public class ChatService
{
    private readonly IChatRepository _chatRepository;
    private readonly IMapper _mapper;

    public ChatService(IChatRepository chatRepository, IMapper mapper)
    {
        _chatRepository = chatRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// Получить сообщение по Id
    /// </summary>
    public async Task<ChatMessageResponse> GetById(Guid id)
    {
        var message = await _chatRepository.GetById(id);

        if (message is null)
            throw new NotFoundException("Сообщение не найдено.");

        return _mapper.Map<ChatMessageResponse>(message);
    }

    /// <summary>
    /// Получить список сообщений
    /// </summary>
    public async Task<List<ChatMessageResponse>> Get(int limit)
    {
        var list = await _chatRepository.Get(limit);

        return _mapper.Map<List<ChatMessageResponse>>(list);
    }

    /// <summary>
    /// Отправить сообщение
    /// </summary>
    public async Task<ChatMessageResponse> SendMessage(SendMessageRequest request)
    {
        var message = new ChatMessage(
            request.SenderId,
            request.ReceiverId,
            request.Content);

        await _chatRepository.Add(message);

        return _mapper.Map<ChatMessageResponse>(message);
    }

    /// <summary>
    /// Получить диалог
    /// </summary>
    public async Task<DialogResponse> GetDialog(Guid user1, Guid user2)
    {
        var messages = await _chatRepository.GetDialog(user1, user2);

        var mapped = _mapper.Map<List<ChatMessageResponse>>(messages);

        return new DialogResponse(mapped);
    }

    /// <summary>
    /// Пометить сообщение прочитанным
    /// </summary>
    public async Task MarkAsRead(Guid messageId)
    {
        var message = await _chatRepository.GetById(messageId);

        if (message is null)
            throw new NotFoundException("Сообщение не найдено.");

        message.MarkAsRead();

        _chatRepository.Update(message);
    }

    /// <summary>
    /// Удалить сообщение
    /// </summary>
    public async Task Delete(Guid messageId)
    {
        var message = await _chatRepository.GetById(messageId);

        if (message is null)
            throw new NotFoundException("Сообщение не найдено.");

        _chatRepository.Delete(message);
    }
}
