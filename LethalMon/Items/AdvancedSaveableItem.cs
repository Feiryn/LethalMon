using Newtonsoft.Json;

namespace LethalMon.Items;

public interface IAdvancedSaveableItem
{
    public object GetAdvancedItemDataToSave();
    
    public void LoadAdvancedItemData(object data);
}