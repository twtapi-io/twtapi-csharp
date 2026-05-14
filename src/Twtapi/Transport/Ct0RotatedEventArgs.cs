namespace Twtapi;

/// <summary>
/// Payload for <see cref="TwtApi.Ct0Rotated"/>. Fired after the SDK
/// detects a fresh <c>ct0</c> in the <c>X-Twitter-New-Ct0</c> response
/// header and stores it in <see cref="TwtApi.Cookies"/>.
/// </summary>
/// <param name="NewCt0">The rotated <c>ct0</c> value. Persist if you need to resume the session later.</param>
public sealed record Ct0RotatedEventArgs(string NewCt0);
