using Commerce.Application.Models;

namespace Commerce.Application.Abstractions;

public interface ICommerceRepository
{
    /// <summary>Inserts all rows or none. Throws ConflictException if a key already exists.</summary>
    Task<int> CreateAsync(string sourceFile, IReadOnlyList<CommerceRow> rows, CancellationToken cancellationToken);

    /// <summary>Moves the invalid rows of one date to quarantine and returns how many moved.</summary>
    Task<int> ProcessAsync(DateOnly processDate, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommerceRecord>> ListAsync(DateOnly? processDate, CancellationToken cancellationToken);

    Task<IReadOnlyList<QuarantineRecord>> ListQuarantineAsync(DateOnly? processDate, CancellationToken cancellationToken);
}
