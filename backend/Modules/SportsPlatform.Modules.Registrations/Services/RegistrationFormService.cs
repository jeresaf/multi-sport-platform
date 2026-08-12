using System.Text.Json;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Registrations.Entities;
namespace SportsPlatform.Modules.Registrations.Services;

public interface IRegistrationFormService
{
    // Form Management
    Task<RegistrationForm> CreateFormAsync(Guid tenantId, string name, string createdBy, Guid? competitionId = null, string? description = null, CancellationToken cancellationToken = default);
    Task<RegistrationForm?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RegistrationForm>> GetFormsByTenantAsync(Guid tenantId, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<RegistrationForm>> GetFormsByCompetitionAsync(Guid competitionId, CancellationToken cancellationToken = default);
    Task<RegistrationForm> UpdateFormSchemaAsync(Guid formId, List<RegistrationFormField> fields, CancellationToken cancellationToken = default);
    Task<RegistrationForm> UpdateFormAsync(Guid formId, string? name = null, string? description = null, CancellationToken cancellationToken = default);
    Task<RegistrationForm> ActivateFormAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<RegistrationForm> DeactivateFormAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<RegistrationForm> CloneFormAsync(Guid formId, string newName, string createdBy, CancellationToken cancellationToken = default);

    // Form Submission Management
    Task<RegistrationFormSubmission> CreateSubmissionAsync(Guid formId, Guid participantId, Guid? contestId = null, CancellationToken cancellationToken = default);
    Task<RegistrationFormSubmission?> GetSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RegistrationFormSubmission>> GetSubmissionsByFormAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RegistrationFormSubmission>> GetSubmissionsByParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<RegistrationFormSubmission> UpdateSubmissionDataAsync(Guid submissionId, Dictionary<string, object> formData, CancellationToken cancellationToken = default);
    Task<RegistrationFormSubmission> SubmitFormAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<RegistrationFormSubmission> ApproveSubmissionAsync(Guid submissionId, Guid reviewerId, CancellationToken cancellationToken = default);
    Task<RegistrationFormSubmission> RejectSubmissionAsync(Guid submissionId, Guid reviewerId, string? notes = null, CancellationToken cancellationToken = default);
    Task<RegistrationFormSubmission> RequestChangesAsync(Guid submissionId, Guid reviewerId, string? notes = null, CancellationToken cancellationToken = default);
    Task<RegistrationFormSubmission> StartReviewAsync(Guid submissionId, Guid reviewerId, CancellationToken cancellationToken = default);

    // Validation
    Task<(bool IsValid, List<string> Errors)> ValidateSubmissionAsync(Guid formId, Dictionary<string, object> formData, CancellationToken cancellationToken = default);
}

public class RegistrationFormService : IRegistrationFormService
{
    private readonly IRepository<RegistrationForm> _formRepository;
    private readonly IRepository<RegistrationFormSubmission> _submissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegistrationFormService(
        IRepository<RegistrationForm> formRepository,
        IRepository<RegistrationFormSubmission> submissionRepository,
        IUnitOfWork unitOfWork)
    {
        _formRepository = formRepository;
        _submissionRepository = submissionRepository;
        _unitOfWork = unitOfWork;
    }

    #region Form Management

    public async Task<RegistrationForm> CreateFormAsync(
        Guid tenantId,
        string name,
        string createdBy,
        Guid? competitionId = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var form = new RegistrationForm(
            Guid.NewGuid(),
            tenantId,
            name,
            createdBy,
            competitionId,
            description);

        await _formRepository.AddAsync(form, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return form;
    }

    public async Task<RegistrationForm?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        return await _formRepository.GetByIdAsync(formId, cancellationToken);
    }

    public async Task<IEnumerable<RegistrationForm>> GetFormsByTenantAsync(
        Guid tenantId,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        return await _formRepository.FindAsync(
            f => f.TenantId == tenantId && (!isActive.HasValue || f.IsActive == isActive.Value),
            cancellationToken);
    }

    public async Task<IEnumerable<RegistrationForm>> GetFormsByCompetitionAsync(
        Guid competitionId,
        CancellationToken cancellationToken = default)
    {
        return await _formRepository.FindAsync(
            f => f.CompetitionId == competitionId,
            cancellationToken);
    }

    public async Task<RegistrationForm> UpdateFormSchemaAsync(
        Guid formId,
        List<RegistrationFormField> fields,
        CancellationToken cancellationToken = default)
    {
        var form = await _formRepository.GetByIdAsync(formId, cancellationToken)
            ?? throw new InvalidOperationException($"Form with ID {formId} not found.");

        var schemaJson = JsonSerializer.Serialize(RegistrationFormSchema.Normalize(fields));
        form.UpdateSchema(schemaJson);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return form;
    }

    public async Task<RegistrationForm> UpdateFormAsync(
        Guid formId,
        string? name = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var form = await _formRepository.GetByIdAsync(formId, cancellationToken)
            ?? throw new InvalidOperationException($"Form with ID {formId} not found.");

        if (name != null)
            form.UpdateName(name);

        if (description != null)
            form.UpdateDescription(description);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return form;
    }

    public async Task<RegistrationForm> ActivateFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await _formRepository.GetByIdAsync(formId, cancellationToken)
            ?? throw new InvalidOperationException($"Form with ID {formId} not found.");

        form.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return form;
    }

    public async Task<RegistrationForm> DeactivateFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await _formRepository.GetByIdAsync(formId, cancellationToken)
            ?? throw new InvalidOperationException($"Form with ID {formId} not found.");

        form.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return form;
    }

    public async Task<RegistrationForm> CloneFormAsync(
        Guid formId,
        string newName,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var originalForm = await _formRepository.GetByIdAsync(formId, cancellationToken)
            ?? throw new InvalidOperationException($"Form with ID {formId} not found.");

        var clonedForm = new RegistrationForm(
            Guid.NewGuid(),
            originalForm.TenantId,
            newName,
            createdBy,
            originalForm.CompetitionId,
            originalForm.Description);

        clonedForm.UpdateSchema(originalForm.FormSchema);

        await _formRepository.AddAsync(clonedForm, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return clonedForm;
    }

    #endregion

    #region Form Submission Management

    public async Task<RegistrationFormSubmission> CreateSubmissionAsync(
        Guid formId,
        Guid participantId,
        Guid? contestId = null,
        CancellationToken cancellationToken = default)
    {
        var form = await _formRepository.GetByIdAsync(formId, cancellationToken)
            ?? throw new InvalidOperationException($"Form with ID {formId} not found.");
        if (!form.IsActive)
            throw new InvalidOperationException("Submissions cannot be created for an inactive form.");
        var submission = new RegistrationFormSubmission(
            Guid.NewGuid(),
            formId,
            participantId,
            form.Version,
            form.FormSchema,
            contestId);

        await _submissionRepository.AddAsync(submission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return submission;
    }

    public async Task<RegistrationFormSubmission?> GetSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        return await _submissionRepository.GetByIdAsync(submissionId, cancellationToken);
    }

    public async Task<IEnumerable<RegistrationFormSubmission>> GetSubmissionsByFormAsync(
        Guid formId,
        CancellationToken cancellationToken = default)
    {
        return await _submissionRepository.FindAsync(
            s => s.FormId == formId,
            cancellationToken);
    }

    public async Task<IEnumerable<RegistrationFormSubmission>> GetSubmissionsByParticipantAsync(
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        return await _submissionRepository.FindAsync(
            s => s.ParticipantId == participantId,
            cancellationToken);
    }

    public async Task<RegistrationFormSubmission> UpdateSubmissionDataAsync(
        Guid submissionId,
        Dictionary<string, object> formData,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission with ID {submissionId} not found.");

        if (submission.Status != RegistrationFormSubmissionStatus.Draft &&
            submission.Status != RegistrationFormSubmissionStatus.ChangesRequested)
            throw new InvalidOperationException("Only draft or changes-requested submissions can be updated.");

        var formDataJson = JsonSerializer.Serialize(formData);
        submission.UpdateFormData(formDataJson);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return submission;
    }

    public async Task<RegistrationFormSubmission> SubmitFormAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission with ID {submissionId} not found.");

        // Validate before submitting
        var formData = JsonSerializer.Deserialize<Dictionary<string, object>>(submission.FormData) ?? new();
        var (isValid, errors) = RegistrationFormSchema.Validate(
            submission.FormSchemaSnapshot, formData);

        if (!isValid)
            throw new InvalidOperationException($"Form validation failed: {string.Join(", ", errors)}");

        submission.Submit();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return submission;
    }

    public async Task<RegistrationFormSubmission> ApproveSubmissionAsync(
        Guid submissionId,
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission with ID {submissionId} not found.");

        submission.SetReviewer(reviewerId);
        submission.Approve();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return submission;
    }

    public async Task<RegistrationFormSubmission> RejectSubmissionAsync(
        Guid submissionId,
        Guid reviewerId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission with ID {submissionId} not found.");

        submission.SetReviewer(reviewerId);
        submission.Reject(notes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return submission;
    }

    public async Task<RegistrationFormSubmission> RequestChangesAsync(
        Guid submissionId,
        Guid reviewerId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission with ID {submissionId} not found.");

        submission.SetReviewer(reviewerId);
        submission.RequestChanges(notes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return submission;
    }

    public async Task<RegistrationFormSubmission> StartReviewAsync(
        Guid submissionId,
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission with ID {submissionId} not found.");

        submission.SetReviewer(reviewerId);
        submission.UnderReview();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return submission;
    }

    #endregion

    #region Validation

    public async Task<(bool IsValid, List<string> Errors)> ValidateSubmissionAsync(
        Guid formId,
        Dictionary<string, object> formData,
        CancellationToken cancellationToken = default)
    {
        var form = await _formRepository.GetByIdAsync(formId, cancellationToken);
        if (form == null)
            return (false, new List<string> { "Form not found" });

        return RegistrationFormSchema.Validate(form.FormSchema, formData);
    }

    #endregion
}
