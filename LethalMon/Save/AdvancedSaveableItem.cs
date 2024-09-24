namespace LethalMon.Save;

public interface IAdvancedSaveableItem
{
    public object GetAdvancedItemDataToSave();
    
    public void LoadAdvancedItemData(object data);
}