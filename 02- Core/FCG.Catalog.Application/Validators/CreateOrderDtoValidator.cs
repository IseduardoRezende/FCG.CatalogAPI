using FCG.Catalog.Application.DTOs.Orders;
using FluentValidation;

namespace FCG.Catalog.Application.Validators;

public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.GameId).GreaterThan(0);
    }
}
