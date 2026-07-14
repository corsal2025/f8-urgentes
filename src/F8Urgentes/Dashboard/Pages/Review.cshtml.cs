using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class ReviewModel(IUrgentRequestRepository repository) : PageModel
{
    public sealed record FlaggedRow(UrgentRequest Request, IReadOnlyList<ImportFlag> Flags);

    public IReadOnlyList<FlaggedRow> FlaggedRequests { get; private set; } = [];

    public void OnGet()
    {
        FlaggedRequests = repository.GetFlagged()
            .Select(r => new FlaggedRow(r, repository.GetFlagsFor(r.Id)))
            .ToList();
    }
}
