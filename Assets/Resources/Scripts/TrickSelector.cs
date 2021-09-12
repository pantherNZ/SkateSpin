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
        currentCategories = dataHandler.categories;

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

    public void ToggleCategory( string category )
    {
        if( currentCategories.Contains( category ) )
            currentCategories.Remove( category );
        else
            currentCategories.Add( category );

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
        currentCategories = dataHandler.categories;
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
