# Changelog

All notable changes to `Twtapi` will land here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] — 2026-05-14

### Added
- Initial release.
- Wraps every public twtapi.io endpoint:
  - User reads: `/user`, `/id_by_username`, `/username_by_id`, `/followers`, `/user_tweets`.
  - Tweet reads: `/retweets`, `/quotes`, `/comments`, `/reply_ids`, `/search`.
  - Engagement: `/tweet`, `/comment`, `/like`, `/retweet`, `/bookmark`, `/delete_tweet`, `/follow`.
  - Login: `/login/start`, `/login/2fa`, `/login/email_code`, `/csrf_token`, `/screen_name_from_token`.
  - Account: `/change_password` (with automatic cookie rotation).
  - Media: `/upload_media`.
  - Communities: `/community_info`, `/community_check_member`,
    `/community_members`, `/community_join`, `/community_leave`,
    `/community_request_join`.
- `IAsyncEnumerable<JsonElement>` iterators for every paginated endpoint.
- Community member iterator flattens `members_by_role` and tags each
  user with a `role` field.
- Typed exception hierarchy (`BadRequestException`,
  `AuthenticationException`, `BillingException`, `PermissionException`,
  `NotFoundException`, `RequestTimeoutException`, `ValidationException`
  with `DuplicateTweetException` / `TweetTooLongException` derivatives,
  `RateLimitException`, `InternalException`, `UpstreamException`,
  `ServiceUnavailableException`, `NetworkException`).
- Automatic `ct0` rotation via `X-Twitter-New-Ct0`, exposed through the
  `Ct0Rotated` event and `client.Cookies.Ct0`.
- `ChangePasswordAsync` auto-rotates the held `auth_token` + `ct0` pair.
- Rate-limit snapshot at `client.LastRateLimit`.
- Inline retry policy: 429 / 408 / 5xx retried on idempotent endpoints,
  never on `POST /tweet` or `POST /comment`.
- Optional structured logging via `Microsoft.Extensions.Logging.ILogger`.

[Unreleased]: https://github.com/twtapi-io/twtapi-csharp/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/twtapi-io/twtapi-csharp/releases/tag/v0.1.0
