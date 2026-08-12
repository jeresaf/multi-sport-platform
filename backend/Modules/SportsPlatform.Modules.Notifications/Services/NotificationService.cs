using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Notifications.Entities;

namespace SportsPlatform.Modules.Notifications.Services;

public interface INotificationService
{
    // Notification Management
    Task<Notification> CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        NotificationChannel channel,
        string title,
        string content,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Notification>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    // Notification Actions
    Task<Notification> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Notification> MarkAsSentAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<Notification> MarkAsFailedAsync(Guid notificationId, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task<Notification> RetryAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task DeleteNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default);

    // Bulk Operations
    Task<IEnumerable<Notification>> GetPendingNotificationsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task SendBatchAsync(IEnumerable<Guid> notificationIds, CancellationToken cancellationToken = default);

    // Templates
    Task<NotificationTemplate> CreateTemplateAsync(
        Guid tenantId,
        string name,
        NotificationType type,
        NotificationChannel channel,
        string titleTemplate,
        string contentTemplate,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplate?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<NotificationTemplate>> GetTemplatesByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<NotificationTemplate> UpdateTemplateAsync(
        Guid templateId,
        string? name = null,
        string? titleTemplate = null,
        string? contentTemplate = null,
        CancellationToken cancellationToken = default);

    // Notification Generation from Templates
    Task<Notification> SendFromTemplateAsync(
        Guid userId,
        Guid templateId,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default);
}

public class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<NotificationTemplate> _templateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(
        IRepository<Notification> notificationRepository,
        IRepository<NotificationTemplate> templateRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _templateRepository = templateRepository;
        _unitOfWork = unitOfWork;
    }

    #region Notification Management

    public async Task<Notification> CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        NotificationChannel channel,
        string title,
        string content,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification(
            Guid.NewGuid(),
            userId,
            type,
            channel,
            title,
            content);

        if (!string.IsNullOrEmpty(metadata))
        {
            notification.SetMetadata(metadata);
        }

        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public async Task<Notification?> GetNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        return await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
    }

    public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var allNotifications = await _notificationRepository.FindAsync(
            n => n.UserId == userId,
            cancellationToken);

        return allNotifications
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _notificationRepository.FindAsync(
            n => n.UserId == userId && n.Status != NotificationStatus.Read,
            cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _notificationRepository.FindAsync(
            n => n.UserId == userId && n.Status != NotificationStatus.Read,
            cancellationToken);

        return unreadNotifications.Count();
    }

    #endregion

    #region Notification Actions

    public async Task<Notification> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification with ID {notificationId} not found.");

        notification.MarkAsRead();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _notificationRepository.FindAsync(
            n => n.UserId == userId && n.Status != NotificationStatus.Read,
            cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.MarkAsRead();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Notification> MarkAsSentAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification with ID {notificationId} not found.");

        notification.MarkAsSent();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public async Task<Notification> MarkAsFailedAsync(
        Guid notificationId,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification with ID {notificationId} not found.");

        notification.MarkAsFailed(errorMessage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public async Task<Notification> RetryAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification with ID {notificationId} not found.");

        notification.Retry();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public async Task DeleteNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification with ID {notificationId} not found.");

        await _notificationRepository.DeleteAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Bulk Operations

    public async Task<IEnumerable<Notification>> GetPendingNotificationsAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var pendingNotifications = await _notificationRepository.FindAsync(
            n => n.Status == NotificationStatus.Pending,
            cancellationToken);

        return pendingNotifications
            .OrderBy(n => n.CreatedAt)
            .Take(batchSize)
            .ToList();
    }

    public async Task SendBatchAsync(
        IEnumerable<Guid> notificationIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var notificationId in notificationIds)
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
            if (notification != null)
            {
                // In a real implementation, this would call the appropriate channel service
                // (Email, SMS, Push, etc.) and handle the result
                try
                {
                    // Simulate sending - in production, this would integrate with actual services
                    notification.MarkAsSent();
                }
                catch (Exception ex)
                {
                    notification.MarkAsFailed(ex.Message);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Templates

    public async Task<NotificationTemplate> CreateTemplateAsync(
        Guid tenantId,
        string name,
        NotificationType type,
        NotificationChannel channel,
        string titleTemplate,
        string contentTemplate,
        CancellationToken cancellationToken = default)
    {
        var template = new NotificationTemplate(
            Guid.NewGuid(),
            tenantId,
            name,
            type,
            channel,
            titleTemplate,
            contentTemplate);

        await _templateRepository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return template;
    }

    public async Task<NotificationTemplate?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return await _templateRepository.GetByIdAsync(templateId, cancellationToken);
    }

    public async Task<IEnumerable<NotificationTemplate>> GetTemplatesByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _templateRepository.FindAsync(
            t => t.TenantId == tenantId,
            cancellationToken);
    }

    public async Task<NotificationTemplate> UpdateTemplateAsync(
        Guid templateId,
        string? name = null,
        string? titleTemplate = null,
        string? contentTemplate = null,
        CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException($"Template with ID {templateId} not found.");

        if (name != null)
            template.SetName(name);

        if (titleTemplate != null)
            template.SetTitleTemplate(titleTemplate);

        if (contentTemplate != null)
            template.SetContentTemplate(contentTemplate);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return template;
    }

    public async Task<Notification> SendFromTemplateAsync(
        Guid userId,
        Guid templateId,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException($"Template with ID {templateId} not found.");

        var title = ReplacePlaceholders(template.TitleTemplate, placeholders);
        var content = ReplacePlaceholders(template.ContentTemplate, placeholders);

        return await CreateNotificationAsync(
            userId,
            template.Type,
            template.Channel,
            title,
            content,
            cancellationToken: cancellationToken);
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, string> placeholders)
    {
        var result = template;
        foreach (var placeholder in placeholders)
        {
            result = result.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value);
        }
        return result;
    }

    #endregion
}
