namespace Library.Domain.Exceptions
{
    /// <summary>Base for failures the caller is allowed to see.</summary>
    public abstract class DomainException : Exception
    {
        protected DomainException(string message) : base(message)
        {
        }
    }

    /// <summary>Input that can never be valid. Maps to InvalidArgument / 400.</summary>
    public sealed class ValidationException : DomainException
    {
        public ValidationException(string message) : base(message)
        {
        }
    }

    /// <summary>The entity does not exist. Maps to NotFound / 404.</summary>
    /// <remarks>
    /// One constructor on purpose: every "not found" in this codebase knows the entity and the id,
    /// so the message reads the same way on every route and nobody has to compose it by hand.
    /// </remarks>
    public sealed class NotFoundException : DomainException
    {
        public NotFoundException(string entity, int id) : base($"{entity} {id} was not found.")
        {
        }
    }

    /// <summary>Valid request, incompatible world. Maps to FailedPrecondition / 409.</summary>
    public sealed class ConflictException : DomainException
    {
        public ConflictException(string message) : base(message)
        {
        }
    }
}
