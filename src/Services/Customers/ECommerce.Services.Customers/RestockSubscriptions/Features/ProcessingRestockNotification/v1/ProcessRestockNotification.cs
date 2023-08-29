using BuildingBlocks.Abstractions.CQRS.Commands;
using BuildingBlocks.Core.Extensions;
using ECommerce.Services.Customers.RestockSubscriptions.Features.SendingRestockNotification.v1;
using ECommerce.Services.Customers.Shared.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Customers.RestockSubscriptions.Features.ProcessingRestockNotification.v1;

public record ProcessRestockNotification(long ProductId, int CurrentStock) : ITxCommand
{
    public static ProcessRestockNotification Of(long productId, int currentStock)
    {
        productId.NotBeNegativeOrZero();
        currentStock.NotBeNegativeOrZero();

        return new ProcessRestockNotification(productId, currentStock);
    }
}

internal class ProcessRestockNotificationValidator : AbstractValidator<ProcessRestockNotification>
{
    public ProcessRestockNotificationValidator()
    {
        RuleFor(x => x.CurrentStock).NotEmpty();

        RuleFor(x => x.ProductId).NotEmpty();
    }
}

internal class ProcessRestockNotificationHandler : ICommandHandler<ProcessRestockNotification>
{
    private readonly CustomersDbContext _customersDbContext;
    private readonly ICommandProcessor _commandProcessor;
    private readonly ILogger<ProcessRestockNotificationHandler> _logger;

    public ProcessRestockNotificationHandler(
        CustomersDbContext customersDbContext,
        ICommandProcessor commandProcessor,
        ILogger<ProcessRestockNotificationHandler> logger
    )
    {
        _customersDbContext = customersDbContext;
        _commandProcessor = commandProcessor;
        _logger = logger;
    }

    public async Task<Unit> Handle(ProcessRestockNotification command, CancellationToken cancellationToken)
    {
        command.NotBeNull();

        var subscribedCustomers = _customersDbContext.RestockSubscriptions.Where(
            x => x.ProductInformation.Id == command.ProductId && !x.Processed
        );

        if (!await subscribedCustomers.AnyAsync(cancellationToken: cancellationToken))
            return Unit.Value;

        foreach (var restockSubscription in subscribedCustomers)
        {
            restockSubscription!.MarkAsProcessed(DateTime.Now);

            // https://github.com/kgrzybek/modular-monolith-with-ddd#38-internal-processing
            // schedule `SendRestockNotification` for running as a internal command after commenting transaction
            await _commandProcessor.ScheduleAsync(
                new SendRestockNotification(restockSubscription.Id, command.CurrentStock),
                cancellationToken
            );
        }

        await _customersDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Marked restock subscriptions as processed");

        return Unit.Value;
    }
}
