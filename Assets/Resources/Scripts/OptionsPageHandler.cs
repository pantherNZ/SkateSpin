using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

class OptionsPageHandler : IBasePage, IEventReceiver
{
    [Serializable]
    public class CategoryButton
    {
        public Toggle toggle = null;
        public string category = string.Empty;
    }

    [SerializeField] RectTransform optionsPanel = null;
    [SerializeField] float topHeight = -420.0f;
    [SerializeField] float roundUpHeightOffset = 100.0f;
    [SerializeField] float moveTimeSec = 0.25f;
    [SerializeField] List<CategoryButton> toggles = new List<CategoryButton>();
    [SerializeField] Toggle shortTrickNamesToggle = null;
    [SerializeField] Toggle canPickLandedTricksToggle = null;
    [SerializeField] GameObject pullTab = null;
    [SerializeField] GameObject hideMenuButton = null;

    TrickSelectorPage trickSelector;
    float bottomHeight;
    float moveSpeed;

    private void Awake()
    {
        EventSystem.Instance.AddSubscriber( this );
        trickSelector = FindObjectOfType<TrickSelectorPage>();
        var root = FindObjectOfType<PageNavigator>();
        var yMult = root.GetScaleMultiplier( true );
        roundUpHeightOffset *= yMult;
        topHeight *= yMult;
        moveSpeed = Mathf.Abs( topHeight - bottomHeight ) / moveTimeSec;
    }

    private void Start()
    {
        bottomHeight = optionsPanel.anchoredPosition.y;

        foreach( var toggle in toggles )
        {
            toggle.toggle.onValueChanged.AddListener( ( value ) =>
            {
                trickSelector.ToggleCategory( toggle.toggle, toggle.category );
            } );
        }

        shortTrickNamesToggle.onValueChanged.AddListener( ( on ) =>
        {
            EventSystem.Instance.TriggerEvent( new UseShortTrickNamesEvent() { value = on }, this );
        } );

        canPickLandedTricksToggle.onValueChanged.AddListener( ( on ) =>
        {
            EventSystem.Instance.TriggerEvent( new CanPickLandedTricksEvent() { value = on }, this );
        } );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is UseShortTrickNamesEvent useShortTrickNamesEvent )
        {
            shortTrickNamesToggle.SetIsOnWithoutNotify( useShortTrickNamesEvent.value );
        }
        else if( e is CanPickLandedTricksEvent canPickLandedTricksEvent )
        {
            canPickLandedTricksToggle.SetIsOnWithoutNotify( canPickLandedTricksEvent.value );
        }
        else if( e is DataLoadedEvent )
            Initialise();
    }

    private void Initialise()
    {
        foreach( var toggle in toggles )
        {
            toggle.toggle.SetIsOnWithoutNotify( trickSelector.CurrentCategories.Contains( toggle.category ) );
            toggle.toggle.GetComponentInChildren<Text>().color = toggle.toggle.isOn ? new Color( 0XE6 / 255.0f, 0XEE / 255.0f, 0XF8 / 255.0f ) : new Color( 0X98 / 255.0f, 0X98 / 255.0f, 0X98 / 255.0f );
        }
    }

    private Vector2? dragPos;
    private Vector2 startDragPos;
    private Vector2 startMousePos;
    private float clickTime;

    private void Update()
    {
        if( dragPos != null )
        {
            var yPos = Mathf.Clamp( optionsPanel.anchoredPosition.y  + Input.mousePosition.y - dragPos.Value.y, bottomHeight, topHeight );
            optionsPanel.anchoredPosition = optionsPanel.anchoredPosition.SetY( yPos );
            dragPos = Input.mousePosition;
        }

        if( Utility.IsMouseDownOrTouchStart() && Utility.IsPointerOverGameObject( pullTab ) )
        {
            dragPos = startMousePos = Input.mousePosition;
            startDragPos = optionsPanel.anchoredPosition;
            clickTime = Time.time;
        }
        else if( Utility.IsMouseUpOrTouchEnd() && dragPos != null )
        {
            if( ( startMousePos - Input.mousePosition.ToVector2() ).sqrMagnitude < 5.0f * 5.0 && ( Time.time - clickTime ) < 1.0f )
                ToggleOptions();
            else if( startDragPos.y > bottomHeight )
                StartCoroutine( MoveToHeight( optionsPanel.anchoredPosition.y < ( topHeight - roundUpHeightOffset ) ? bottomHeight : topHeight ) );
            else
                StartCoroutine( MoveToHeight( optionsPanel.anchoredPosition.y < ( bottomHeight + roundUpHeightOffset ) ? bottomHeight : topHeight ) );
            dragPos = null;
        }

        if( Utility.IsBackButtonDown() && optionsPanel.anchoredPosition.y >= topHeight )
            StartCoroutine( MoveToHeight( bottomHeight ) );
    }

    public void ToggleOptions()
    {
        StartCoroutine( MoveToHeight( optionsPanel.anchoredPosition.y < topHeight ? topHeight : bottomHeight ) );
    }

    public void HideOptions()
    {
        if( optionsPanel.anchoredPosition.y >= topHeight )
            ToggleOptions();
    }

    private IEnumerator MoveToHeight( float toHeight )
    {
        hideMenuButton.SetActive( toHeight == topHeight  );

        float direction = Mathf.Sign( toHeight - optionsPanel.anchoredPosition.y );

        while( ( direction > 0 && optionsPanel.anchoredPosition.y < toHeight ) ||
               ( direction < 0 && optionsPanel.anchoredPosition.y > toHeight ) )
        {
            var difference = Time.deltaTime * moveSpeed;
            difference = Mathf.Min( difference, Mathf.Abs( toHeight - optionsPanel.anchoredPosition.y ) );
            optionsPanel.anchoredPosition = optionsPanel.anchoredPosition.SetY( optionsPanel.anchoredPosition.y + difference * direction );
            yield return null;
        }

        optionsPanel.anchoredPosition = optionsPanel.anchoredPosition.SetY( toHeight );
    }

    public void OpenSupportSite()
    {
        Application.OpenURL( "https://www.buymeacoffee.com/AlexDenford" );
    }
}
