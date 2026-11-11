namespace MusselDiagram.Internal;

internal static class ElementPermissions
{
    public const DiagramPermissions Unregistered = DiagramPermissions.Select | DiagramPermissions.Move | DiagramPermissions.Delete;

    public static DiagramPermissions Of(DiagramBox box, SchemaIndex index)
    {
        var type = index.FindBoxType(box);
        var permissions = type is null
            ? (box.Permissions ?? Unregistered) & Unregistered
            : box.Permissions ?? type.Permissions;
        return box.Locked ? permissions & DiagramPermissions.Select : permissions;
    }

    public static DiagramPermissions Of(DiagramLink link, SchemaIndex index)
    {
        var type = index.FindLinkType(link.Type);
        var permissions = type is null
            ? (link.Permissions ?? Unregistered) & Unregistered
            : link.Permissions ?? type.Permissions;
        return link.Locked ? permissions & DiagramPermissions.Select : permissions;
    }

    public static bool Allows(DiagramElement element, SchemaIndex index, DiagramPermissions permission) => element switch
    {
        DiagramBox box => (Of(box, index) & permission) != 0,
        DiagramLink link => (Of(link, index) & permission) != 0,
        _ => false
    };
}
