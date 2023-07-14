﻿namespace Replybot.Commands;

public class CommandResponse
{
    public string? Description { get; set; }
    public List<FileAttachment> FileAttachments { get; set; } = new();
    public Embed? Embed { get; set; }
    public IReadOnlyList<IEmote>? Reactions { get; set; }
    public bool StopProcessing { get; set; }
}