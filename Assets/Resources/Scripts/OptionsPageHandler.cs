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
    [SerializeField] GridLayoutGroup gridLayout = null;

    TrickSelectorPage trickSelector;
    private new Camera camera;
    float bottomHeight;
    float moveSpeed;

    private void Awake()
    {
        EventSystem.Instance.AddSubscriber( this );
        trickSelector = FindObjectOfType<TrickSelectorPage>();
        camera = Camera.main;
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

        RecalculateGridLayout();
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
    }

    public override void OnShown()
    {
        base.OnShown();

        foreach( var toggle in toggles )
            toggle.toggle.SetIsOnWithoutNotify( trickSelector.CurrentCategories.Contains( toggle.category ) );
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
        else if( Utility.IsMouseUpOrTouchEnd() )
        {
            if( ( startMousePos - Input.mousePosition.ToVector2() ).sqrMagnitude < 5.0f * 5.0 && ( Time.time - clickTime ) < 1.0f )
                ToggleOptions();
            else if( startDragPos.y > bottomHeight )
                StartCoroutine( MoveToHeight( optionsPanel.anchoredPosition.y < ( topHeight - roundUpHeightOffset ) ? bottomHeight : topHeight ) );
            else
                StartCoroutine( MoveToHeight( optionsPanel.anchoredPosition.y < ( bottomHeight + roundUpHeightOffset ) ? bottomHeight : topHeight ) );

            dragPos = null;
        }
    }

    public void ToggleOptions()
    {
        StartCoroutine( MoveToHeight( optionsPanel.anchoredPosition.y < topHeight ? topHeight : bottomHeight ) );
    }

    private IEnumerator MoveToHeight( float toHeight )
    {
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

    void RecalculateGridLayout()
    {
        if( gridLayout != null )
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

            int count = gridLayout.transform.childCount;

            Vector3 cellSize = gridLayout.cellSize;
            Vector3 spacing = gridLayout.spacing;

            var amountPerRow = 2;
            int amountPerColumn = count / amountPerRow;

            float childWidth = ( ( gridLayout.transform as RectTransform ).rect.width - spacing.x * ( amountPerRow - 1 ) ) / amountPerRow;
            float childHeight = ( ( gridLayout.transform as RectTransform ).rect.height - spacing.y * ( amountPerColumn - 1 ) ) / amountPerColumn;

            cellSize.x *= Camera.main.pixelWidth / 1080.0f;
            cellSize.y *= Camera.main.pixelHeight / 1920.0f;

            gridLayout.cellSize = cellSize;
        }
    }

}
