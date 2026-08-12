using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Notifications.Entities;

public class NotificationTemplate : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string TitleTemplate { get; private set; } = string.Empty;
    public string ContentTemplate { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private NotificationTemplate() { }

    public NotificationTemplate(
        Guid id,
        Guid tenantId,
        string name,
        NotificationType type,
        NotificationChannel channel,
        string titleTemplate,
        string contentTemplate) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Type = type;
        Channel = channel;
        TitleTemplate = titleTemplate;
        ContentTemplate = contentTemplate;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetName(string name)
    {
        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTitleTemplate(string titleTemplate)
    {
        TitleTemplate = titleTemplate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetContentTemplate(string contentTemplate)
    {
        ContentTemplate = contentTemplate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}