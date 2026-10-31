namespace Crephore.Api.Common.Constants;

// When guests see a website section: always, only before the event starts, or only after it. Every section is
// Always for now (the editor has no control for it yet, and the public site doesn't apply it yet).
// Stored and serialized as its name; never rename a value once sections use it.
public enum SectionVisibility
{
    Always,
    BeforeEvent,
    AfterEvent,
}
