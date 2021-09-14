using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class TrickSelector : MonoBehaviour, ISavableComponent
{
    public List<DataHandler.TrickEntry> currentTrickList = new List<DataHandler.TrickEntry>();
    public List<string> currentCategories = new List<string>();
    
    [SerializeField] private Text currentTrickText = null;
    [SerializeField] private MinMaxSlider difficultySlider = null;
    [SerializeField] private DataHandler dataHandler = null;
    private int index;

    private bool allowLandedTricksToBeSelected;

    private void Start()
    {
        if( dataHandler.IsDataLoaded )
            Initialise();
        else
            dataHandler.OnDataLoaded += () => Initialise();

        dataHandler.OnResetSaveData += ResetSaveData;
    }

    void Initialise()
    {
        // Setup categories
        currentCategories = new List<string>( dataHandler.categories );

        // Setup trick list
        RecalculateCurrentTrickList();

        // Initialise first trick
        NextTrick();
    }

    public void RecalculateCurrentTrickList()
    {
        currentTrickList.Clear();

        // Pull difficulty from slider
        int minDifficulty = Mathf.RoundToInt( difficultySlider.GetMinValue() );
        int maxDifficulty = Mathf.RoundToInt( difficultySlider.GetMaxValue() );

        foreach( var trick in dataHandler.trickData )
        {
            if( trick.difficulty < minDifficulty || trick.difficulty > maxDifficulty )
                continue;

            if( !currentCategories.Contains( trick.category ) )
                continue;

            if( trick.banned )
                continue;

            if( trick.landed && !allowLandedTricksToBeSelected )
                continue;

            currentTrickList.Add( trick );
        }

        RandomiseTrickList();
    }

    bool inCallback;

    public void ToggleCategory( Toggle toggle, string category )
    {
        if( inCallback )
            return;

        if( !dataHandler.categories.Contains( category ) )
        {
            Debug.LogError( "Invalid cateogry specified: '" + category + "' from " + toggle.name );
            return;
        }

        if( toggle.isOn && !currentCategories.Contains( category ) )
        {
            currentCategories.Add( category );
        }
        else if( !toggle.isOn && currentCategories.Contains( category ) )
        {
            // Don't allow disabling the last category
            if( currentCategories.Count == 1 )
            {
                inCallback = true;
                toggle.isOn = !toggle.isOn;
                inCallback = false;
                return;
            }

            currentCategories.Remove( category );
        }

        dataHandler.Save();
    }

    public void NextTrick()
    {
        // TODO: Play animation / visual
        index = ( index + 1 ) % currentTrickList.Count;
        currentTrickText.text = currentTrickList[index].displayName;
    }

    public void PreviousTrick()
    {
        // TODO: Play animation / visual
        index = ( index + currentTrickList.Count - 1 ) % currentTrickList.Count;
        currentTrickText.text = currentTrickList[index].displayName;
    }

    public void RandomiseTrickList()
    {
        // TODO: Play animation / visual
        currentTrickList.RandomShuffle();
        currentTrickText.text = currentTrickList[index].displayName;
    }

    public void BanCurrentTrick()
    {
        // TODO: Play animation / visual
        currentTrickList[index].banned = true;
        NextTrick();
        dataHandler.Save();
    }

    public void LandCurrentTrick()
    {
        // TODO: Play animation / visual
        currentTrickList[index].landed = true;
        NextTrick();
        dataHandler.Save();
    }

    public void SetAllowLandedTricksToBeSelected( bool value )
    {
        allowLandedTricksToBeSelected = value;
        dataHandler.Save();
    }

    private void ResetSaveData()
    {
        allowLandedTricksToBeSelected = false;
        currentCategories = new List<string>( dataHandler.categories );
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( allowLandedTricksToBeSelected );
        writer.Write( currentCategories.Count );

        foreach( var category in currentCategories )
            writer.Write( category );
    }

    void ISavableComponent.Deserialise( BinaryReader reader )
    {
        allowLandedTricksToBeSelected = reader.ReadBoolean();
        var count = reader.ReadInt32();

        for( int i = 0; i < count; ++i )
            currentCategories.Add( reader.ReadString() );
    }
}
