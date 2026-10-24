namespace Crephore.Api.Data.IEntities;

public interface IHasCreatedAt
{
    DateTimeOffset CreatedAt { get; set; }
}
