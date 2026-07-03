namespace AVEquipmentManager.Shared.Enums;

/// <summary>
/// Canonical AVAMS user roles per client + team agreement (2026-06-15).
///
/// Per the AV Department Head's directive, the system supports exactly
/// four roles. The Supply Office is deliberately NOT modelled as a user
/// role — they maintain their own ticketing system, and the client does
/// not want AVAMS to substitute for the Supply Office workflow.
///
///   Admin         — AV Department Head. Full access to every module.
///   Supervisor    — Senior AV staff. Can approve transactions but not
///                    manage users or perform terminal disposals.
///   AVStaff       — Operational AV staff. Manages equipment, tickets,
///                    loans; cannot approve transactions or manage users.
///   ITSpecialist  — Per-school IT technician. Only entry point to AVAMS
///                    is submitting a maintenance ticket against a piece
///                    of equipment when the school's own first-tier
///                    ticket system cannot resolve it. Cannot read
///                    inventory, cannot read other ITSpecialists'
///                    tickets, cannot perform any other action.
///
/// Backwards-compatibility note: pre-existing seeded users with the
/// legacy "Staff" or "Student" role strings are remapped as follows by
/// <see cref="Normalize"/>:
///     "Staff"   → AVStaff
///     "Student" → ITSpecialist
/// </summary>
public static class Roles
{
    public const string Admin        = "Admin";
    public const string Supervisor   = "Supervisor";
    public const string AVStaff      = "AVStaff";
    public const string ITSpecialist = "ITSpecialist";

    /// <summary>All four canonical roles.</summary>
    public static readonly string[] All =
    {
        Admin, Supervisor, AVStaff, ITSpecialist
    };

    /// <summary>Roles permitted to manage equipment inventory (CRUD).</summary>
    public static readonly string[] InventoryManagers =
    {
        Admin, Supervisor, AVStaff
    };

    /// <summary>Roles permitted to approve / perform terminal transactions.</summary>
    public static readonly string[] Approvers =
    {
        Admin, Supervisor
    };

    /// <summary>
    /// Normalises a possibly-legacy role string to one of the four canonical
    /// roles. Unknown values fall through unchanged so existing rows are
    /// preserved exactly until manually re-roled by the Admin.
    /// </summary>
    public static string Normalize(string role) => role switch
    {
        "Staff"   => AVStaff,
        "Student" => ITSpecialist,
        _         => role
    };

    /// <summary>True if <paramref name="role"/> is one of the four canonical roles.</summary>
    public static bool IsValid(string role) =>
        role == Admin || role == Supervisor || role == AVStaff || role == ITSpecialist;
}
