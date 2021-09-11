using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrickSelector : MonoBehaviour
{
    public List<DataHandler.TrickEntry> currentTrickList = new List<DataHandler.TrickEntry>();
    public List<string> currentCategories = new List<string>();
    
    [SerializeField] private Dropdown categoriesDropdown = null;
    [SerializeField] private Text currentTrickText = null;
    [SerializeField] private MinMaxSlider difficultySlider = null;

    private DataHandler dataHandler;
    private int index;

    private void Start()
    {
        dataHandler = GetComponent<DataHandler>();

        if( dataHandler.IsDataLoaded )
            Initialise();
        else
            dataHandler.onDataLoaded += () => Initialise();
    }

    void Initialise()
    {
        // Setup categories
        categoriesDropdown.options = new List<Dropdown.OptionData>();
        foreach( var category in dataHandler.categories )
            categoriesDropdown.options.Add( new Dropdown.OptionData( category ) );

        // Setup trick list
        RecalculateCurrentTrickList();

        // Initialise first trick
        NextTrick();
    }

    public void RecalculateCurrentTrickList()
    {
        currentTrickList.Clear();
        currentCategories.Clear();

        // Pull categories from multi-selection
        currentCategories.Add( categoriesDropdown.options[categoriesDropdown.value].text );

        // Pull difficulty from slider
        int minDifficulty = Mathf.RoundToInt( difficultySlider.GetMinValue() );
        int maxDifficulty = Mathf.RoundToInt( difficultySlider.GetMaxValue() );

        foreach( var trick in dataHandler.trickData )
        {
            if( trick.difficulty < minDifficulty || trick.difficulty > maxDifficulty )
                continue;

            if( !currentCategories.Contains( trick.category ) )
                continue;

            currentTrickList.Add( trick );
        }

        RandomiseTrickList();
    }
    
    public void NextTrick()
    {
        index = ( index + 1 ) % currentTrickList.Count;
        currentTrickText.text = currentTrickList[index].displayName;
    }

    public void PreviousTrick()
    {
        index = ( index + currentTrickList.Count - 1 ) % currentTrickList.Count;
        currentTrickText.text = currentTrickList[index].displayName;
    }

    public void RandomiseTrickList()
    {
        currentTrickList.RandomShuffle();
        currentTrickText.text = currentTrickList[index].displayName;
    }
}
