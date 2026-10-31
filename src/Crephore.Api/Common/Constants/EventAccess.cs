namespace Crephore.Api.Common.Constants;

// What a user can do on an event (EventUser.Access). Each value grants exactly one feature,
// except Owner, which grants everything (see EventAccessExtensions.Grants).
// Stored and serialized as its name (e.g. "Owner"); never rename a value once it is stored.
public enum EventAccess
{
    Owner,
}
