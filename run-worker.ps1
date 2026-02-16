# Run the Worker so it consumes messages from RabbitMQ (customer-created, order-created).
# Keep this running in a separate terminal while you use the app.
# Without the Worker, messages stay in the queue (Ready) and are never consumed.

Set-Location $PSScriptRoot
dotnet run --project Worker
