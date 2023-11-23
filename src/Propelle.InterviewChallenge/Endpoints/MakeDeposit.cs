using FastEndpoints;
using Polly;
using Propelle.InterviewChallenge.Application;
using Propelle.InterviewChallenge.Application.Domain;
using Propelle.InterviewChallenge.Application.Domain.Events;

namespace Propelle.InterviewChallenge.Endpoints
{
    public static class MakeDeposit
    {
        public class Request
        {
            public Guid UserId { get; set; }

            public decimal Amount { get; set; }
        }

        public class Response
        {
            public Guid DepositId { get; set; }
        }

        public class Endpoint : Endpoint<Request, Response>
        {
            private readonly PaymentsContext _paymentsContext;
            private readonly Application.EventBus.IEventBus _eventBus;
            
            public Endpoint(
                PaymentsContext paymentsContext,
                Application.EventBus.IEventBus eventBus)
            {
                _paymentsContext = paymentsContext;
                _eventBus = eventBus;
            }

            public override void Configure()
            {
                Post("/api/deposits/{UserId}");
            }

            public override async Task HandleAsync(Request req, CancellationToken ct)
            {

                var deposit = new Deposit(req.UserId, req.Amount);

                _paymentsContext.Deposits.Add(deposit);

                await _paymentsContext.SaveChangesAsync(ct);

                // The event publishing seems to happen additionally even if the API call fails
                // When it fails, we need to retry.
                // 10 retries seem to provide enough resiliency, however in a real life scenario a wait would be introduced as well.
                var result = Polly.Policy
                    .Handle<TransientException>()
                    .RetryAsync(10).ExecuteAsync(async () => await _eventBus.Publish(new DepositMade
                    {
                        Id = deposit.Id
                    }));

                await SendAsync(new Response { DepositId = deposit.Id }, 201, ct);

            }
        }
    }
}
