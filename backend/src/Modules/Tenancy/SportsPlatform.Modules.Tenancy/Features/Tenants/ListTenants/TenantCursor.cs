using System.Buffers.Text;
using System.Text;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

/// <summary>The opaque <c>cursor</c> of the tenant list: the last slug of a page (the sort key), base64url-encoded.</summary>
public static class TenantCursor
{
    public static string Encode(string slug) => Base64Url.EncodeToString(Encoding.ASCII.GetBytes(slug));

    public static bool TryDecode(string? cursor, out string slug)
    {
        slug = string.Empty;
        if (cursor is null || cursor.Length > 64 || !Base64Url.IsValid(cursor))
        {
            return false;
        }

        var decoded = Encoding.ASCII.GetString(Base64Url.DecodeFromChars(cursor));
        if (!TenantSlug.HasValidFormat(decoded) || Encode(decoded) != cursor)
        {
            return false;
        }

        slug = decoded;
        return true;
    }
}
