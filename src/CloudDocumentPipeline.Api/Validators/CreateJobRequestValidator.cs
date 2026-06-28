using CloudDocumentPipeline.Application.Jobs;
using FluentValidation;

namespace CloudDocumentPipeline.Api.Validators;

public sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Type)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.PayloadJson)
            .NotEmpty()
            .Must(BeValidJson)
            .WithMessage("PayloadJson must be valid JSON.");
    }

    private static bool BeValidJson(string payloadJson)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(payloadJson);
            return true;
        }
        catch
        {
            return false;
        }
    }
}