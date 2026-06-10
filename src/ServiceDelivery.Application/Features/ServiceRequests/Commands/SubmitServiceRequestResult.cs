namespace ServiceDelivery.Application.Features.ServiceRequests.Commands;

public record SubmitServiceRequestResult(Guid RequestId, string Status);
