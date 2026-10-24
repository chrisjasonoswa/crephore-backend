namespace Crephore.Api.Data.IEntities;

public interface IHasTimestamp : IHasCreatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}
