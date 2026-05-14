# Contributing

Thanks for your interest in improving the .NET client.

## Setup

```bash
git clone https://github.com/twtapi-io/twtapi-csharp
cd twtapi-csharp
dotnet restore
dotnet build -c Release
dotnet test
```

Requires the .NET 8 SDK. The project targets `net8.0` only.

## Testing against the real API

Set `TWTAPI_KEY` in your shell and run an example:

```bash
export TWTAPI_KEY="tw_..."
dotnet run --project examples/Twtapi.Examples -- quickstart
```

Never commit a real key. The examples read it from the environment.

## Style

- File-scoped namespaces.
- XML doc comments on public types and members (the build will fail otherwise).
- Numeric IDs (`user_id`, `tweet_id`, ...) are always `string`.
- Async methods end in `Async` and take a trailing optional `CancellationToken ct = default`.
- Keep `Directory.Build.props` settings (`Nullable`, `TreatWarningsAsErrors`) green.

## Adding an endpoint

1. Add the wrapper to the appropriate resource class.
2. If it paginates, expose both the single-page method and an `IAsyncEnumerable<T>` iterator using `AsyncPaginator`.
3. Document the wrapped HTTP route in the XML summary (`<c>GET /foo</c>`).
4. Add tests for header / body shape and, where applicable, pagination.

## Pull requests

- `dotnet build -c Release` and `dotnet test` clean.
- One logical change per PR.

## License

By contributing you agree your changes ship under the MIT License (see [`LICENSE`](LICENSE)).
