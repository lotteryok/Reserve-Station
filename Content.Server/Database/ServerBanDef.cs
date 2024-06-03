using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using System.Net.Http;
using System.Text.Json;

namespace Content.Server.Database
{
    public sealed class ServerBanDef
    {
        public int? Id { get; }
        public NetUserId? UserId { get; }
        public (IPAddress address, int cidrMask)? Address { get; }
        public ImmutableArray<byte>? HWId { get; }

        public DateTimeOffset BanTime { get; }
        public DateTimeOffset? ExpirationTime { get; }
        public int? RoundId { get; }
        public TimeSpan PlaytimeAtNote { get; }
        public string Reason { get; }
        public NoteSeverity Severity { get; set; }
        public NetUserId? BanningAdmin { get; }
        public ServerUnbanDef? Unban { get; }

        public ServerBanDef(
            int? id,
            NetUserId? userId,
            (IPAddress, int)? address,
            ImmutableArray<byte>? hwId,
            DateTimeOffset banTime,
            DateTimeOffset? expirationTime,
            int? roundId,
            TimeSpan playtimeAtNote,
            string reason,
            NoteSeverity severity,
            NetUserId? banningAdmin,
            ServerUnbanDef? unban)
        {
            if (userId == null && address == null && hwId == null)
            {
                throw new ArgumentException("Must have at least one of banned user, banned address or hardware ID");
            }

            if (address is { } addr && addr.Item1.IsIPv4MappedToIPv6)
            {
                // Fix IPv6-mapped IPv4 addresses
                // So that IPv4 addresses are consistent between separate-socket and dual-stack socket modes.
                address = (addr.Item1.MapToIPv4(), addr.Item2 - 96);
            }

            Id = id;
            UserId = userId;
            Address = address;
            HWId = hwId;
            BanTime = banTime;
            ExpirationTime = expirationTime;
            RoundId = roundId;
            PlaytimeAtNote = playtimeAtNote;
            Reason = reason;
            Severity = severity;
            BanningAdmin = banningAdmin;
            Unban = unban;
        }

        public string FormatBanMessage(IConfigurationManager cfg, ILocalizationManager loc)
        {
            string expires;
            if (ExpirationTime is { } expireTime)
            {
                var duration = expireTime - BanTime;
                var utc = expireTime.ToUniversalTime();
                expires = loc.GetString("ban-expires", ("duration", duration.TotalMinutes.ToString("N0")), ("time", utc.ToString("f")));
            }
            else
            {
                var appeal = cfg.GetCVar(CCVars.InfoLinksAppeal);
                expires = !string.IsNullOrWhiteSpace(appeal)
                    ? loc.GetString("ban-banned-permanent-appeal", ("link", appeal))
                    : loc.GetString("ban-banned-permanent");
            }

            return $"""
                   {loc.GetString("ban-banned-1")}
                   {loc.GetString("ban-banned-2", ("adminName", GetUsername(BanningAdmin.ToString())))}
                   {loc.GetString("ban-banned-3", ("reason", Reason))}
                   {expires}
                   {loc.GetString("ban-banned-4")}
                   """;
        }

        static string GetUsername(string? userId)
        {
            if (userId == null)
            {
                return "Unknown";
            }

            using (var client = new HttpClient())
            {
                string apiUrl = "https://auth.spacestation14.com/api/query/userid?userid=" + userId;

                HttpResponseMessage response = client.Send(new HttpRequestMessage(HttpMethod.Get, apiUrl));

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var jsonObject = JsonDocument.Parse(jsonResponse).RootElement;

                    return jsonObject.GetProperty("userName").GetString() ?? "Unknown";

                }
                else
                {
                    return "Unknown";
                }
            }
        }
    }
}
