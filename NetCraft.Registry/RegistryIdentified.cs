namespace NetCraft.Registry;

//RegistryIdentified lets a registry element backfill its own identity from its registry name
//Vanilla elements do not carry an id (it lives only in the registry); elements carry it here for network sync and debugging
//Code-registered elements know their id at construction; data-driven elements only get it from the file name at registration time
public interface RegistryIdentified
{
    //SetRegistryId is backfilled by the registry loading flow when writing into the registry
    void SetRegistryId(Identifier id);
}
