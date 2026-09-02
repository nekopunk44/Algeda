using API.Auth;
using Application.DTOs.ChatMessage;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    // Legacy unrestricted chat CRUD is retained only for administration.
    // Client and realtor conversations must use the ownership-aware DealChatsController.
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ApiController]
    [Route("api/chat-messages")]
    public class ChatMessagesController : ApiControllerBase
    {
        private readonly ChatService _service;

        public ChatMessagesController(ChatService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> Get([FromQuery][Range(1, 1000)] int limit = 100)
        {
            return ExecuteAsync(async () => Ok(await _service.Get(limit)));
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [HttpGet("dialog/{user1:guid}/{user2:guid}")]
        public Task<IActionResult> GetDialog(Guid user1, Guid user2)
        {
            return ExecuteAsync(async () => Ok(await _service.GetDialog(user1, user2)));
        }

        [HttpPost]
        public Task<IActionResult> Create([FromBody] SendMessageRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.SendMessage(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPatch("{id:guid}/read")]
        public Task<IActionResult> MarkAsRead(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.MarkAsRead(id);
                return NoContent();
            });
        }

        [HttpDelete("{id:guid}")]
        public Task<IActionResult> Delete(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.Delete(id);
                return NoContent();
            });
        }
    }
}
