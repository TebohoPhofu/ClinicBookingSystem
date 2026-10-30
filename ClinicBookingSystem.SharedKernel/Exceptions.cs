namespace ClinicBookingSystem.SharedKernel;

/// <summary>A business rule was violated (maps to HTTP 422).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>The requested resource does not exist (maps to HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request conflicts with current state, e.g. a double booking (maps to HTTP 409).</summary>
public class ConflictException(string message, Exception? inner = null) : Exception(message, inner);
