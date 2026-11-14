using TimeBlock.Core.Contracts;
using TimeBlock.Core.Entities;

namespace TimeBlock.Api.Services;

public static class BlockMapper
{
    public static BlockDto ToDto(Block block, string ownerName) => new(
        block.Id,
        block.OwnerId,
        ownerName,
        block.GroupId,
        block.TemplateId,
        block.Title,
        block.Category,
        block.Privacy,
        block.Status,
        block.StartUtc,
        block.EndUtc,
        block.ConfirmedAt);

    public static BlockDto ToDto(Block block, string ownerName, Guid viewerId) =>
        block.Privacy == BlockPrivacy.BusyOnly && block.OwnerId != viewerId
            ? ToDto(block, ownerName) with { Title = "Busy", Category = "" }
            : ToDto(block, ownerName);
}
