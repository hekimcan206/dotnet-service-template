namespace ServiceTemplate.Api.Domain;

/// <summary>Lifecycle of an order. Transitions: Draft → Submitted → Cancelled, or Draft → Cancelled.</summary>
public enum OrderStatus
{
    Draft,
    Submitted,
    Cancelled,
}
