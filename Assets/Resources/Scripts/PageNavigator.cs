using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PageNavigator : MonoBehaviour
{
    [SerializeField] private List<GameObject> pages = new List<GameObject>();
    [SerializeField] private GameObject PageNavigationCanvas = null;
    [SerializeField] private RectTransform panel = null;
    [SerializeField] private float centreX = 0.0f;
    [SerializeField] private GameObject confirmDeleteDataPanel = null;
    [SerializeField] private RectTransform horizontalPageLayout = null;
    [SerializeField] private float offsetRoundingWidth = 100.0f;
    [SerializeField] private float pageMoveTimeSec = 1.0f;
    [SerializeField] private float navigatorMoveTimeSec = 0.0f;
    [SerializeField] private Image blurPage = null;
    [SerializeField] private float blurAmount = 1.5f;
    [SerializeField] private float blurSpeed = 3.0f;

    Vector2? dragPos;
    float start_x;
    private const float screenWidth = 1080.0f;
    private float leftX;
    private int currentPage = 0;

    void Start()
    {
        PageNavigationCanvas.SetActive( false );
        confirmDeleteDataPanel.SetActive( false );

        leftX = panel.anchoredPosition.x;
        Utility.FunctionTimer.CreateTimer( 0.01f, () => ShowPage( 0 ) );

        blurPage.material = Instantiate( blurPage.material );
    }

    public void ToggleMenu()
    {
        if( !PageNavigationCanvas.activeSelf )
            PageNavigationCanvas.SetActive( true );
        confirmDeleteDataPanel.SetActive( false );

        StartCoroutine( InterpBlur( panel.anchoredPosition.x < centreX ? blurAmount : 0.0f ) );
        StartCoroutine( MovePanel( panel.anchoredPosition.x < centreX ? centreX : leftX ) );
    }

    private IEnumerator InterpBlur( float targetBlur )
    {
        float currentVal = blurAmount - targetBlur;
        float direction = Mathf.Sign( targetBlur - currentVal );

        while( ( direction > 0 && currentVal < targetBlur ) ||
               ( direction < 0 && currentVal > targetBlur ) )
        {
            var difference = Time.deltaTime * blurSpeed;
            difference = Mathf.Min( difference, Mathf.Abs( targetBlur - currentVal ) );
            currentVal += difference * direction;
            blurPage.material.SetFloat( "_Size", currentVal );
            yield return null;
        }

        currentVal = targetBlur;
        blurPage.material.SetFloat( "_Size", currentVal );
    }

    private IEnumerator MovePanel( float xPos )
    {
        float speed = Mathf.Abs( xPos - panel.anchoredPosition.x ) / navigatorMoveTimeSec;
        float direction = Mathf.Sign( xPos - panel.anchoredPosition.x );

        while( ( direction > 0 && panel.anchoredPosition.x < xPos ) ||
               ( direction < 0 && panel.anchoredPosition.x > xPos ) )
        {
            var difference = Time.deltaTime * speed;
            difference = Mathf.Min( difference, Mathf.Abs( xPos - panel.anchoredPosition.x ) );
            panel.anchoredPosition = panel.anchoredPosition.SetX( panel.anchoredPosition.x + difference * direction );
            yield return null;
        }

        panel.anchoredPosition = panel.anchoredPosition.SetX( xPos );

        if( PageNavigationCanvas.activeSelf && direction == -1 )
            PageNavigationCanvas.SetActive( false );
    }

    public void ShowPage( int index )
    {
        if( currentPage == index )
            return;

        while( currentPage != index )
            ChangePageInstant( true );

        PageNavigationCanvas.SetActive( false );
        panel.anchoredPosition = panel.anchoredPosition.SetX( leftX );
    }

    public void ToggleActive( GameObject obj )
    {
        obj.ToggleActive();
    }

    private void Update()
    {
        if( dragPos != null )
        {
            float val = horizontalPageLayout.anchoredPosition.x + Utility.GetMouseOrTouchPos().x - dragPos.Value.x;
            horizontalPageLayout.anchoredPosition = horizontalPageLayout.anchoredPosition.SetX( Mathf.Clamp( val, start_x - screenWidth, start_x + screenWidth ) );
            dragPos = Utility.GetMouseOrTouchPos();
        }

        if( Utility.IsMouseDownOrTouchStart() )
        {
            var eventData = new UnityEngine.EventSystems.PointerEventData( UnityEngine.EventSystems.EventSystem.current )
            {
                position = Utility.GetMouseOrTouchPos()
            };
            var raycastResults = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll( eventData, raycastResults );

            if( raycastResults.All( x =>
            {
                return x.gameObject.GetComponent<Touchable>() == null &&
                        x.gameObject.GetComponentInParent<Touchable>() == null &&
                        x.gameObject.GetComponent<Button>() == null &&
                        x.gameObject.GetComponentInParent<Button>() == null &&
                        x.gameObject.GetComponent<Toggle>() == null &&
                        x.gameObject.GetComponentInParent<Toggle>() == null &&
                        x.gameObject.GetComponent<Slider>() == null &&
                        x.gameObject.GetComponentInParent<Slider>() == null;
            } ) )
            {
                dragPos = Utility.GetMouseOrTouchPos();
                start_x = screenWidth * Mathf.Floor( horizontalPageLayout.anchoredPosition.x / screenWidth + 0.5f );
            }
        }
        else if( Utility.IsMouseUpOrTouchEnd() )
        {
            FinishDrag();
        }
    }

    private void FinishDrag()
    {
        if( Mathf.Abs( start_x - horizontalPageLayout.anchoredPosition.x ) < offsetRoundingWidth )
            StartCoroutine( MovePage( start_x, false ) );
        else
            StartCoroutine( MovePage( start_x + ( horizontalPageLayout.anchoredPosition.x < start_x ? -screenWidth : screenWidth ), true ) );

        dragPos = null;
    }

    private IEnumerator MovePage( float xPos, bool fixPagesAfter )
    {
        float speed = Mathf.Abs( xPos - horizontalPageLayout.anchoredPosition.x ) / pageMoveTimeSec;
        float direction = Mathf.Sign( xPos - horizontalPageLayout.anchoredPosition.x );

        while( ( direction > 0 && horizontalPageLayout.anchoredPosition.x < xPos ) ||
               ( direction < 0 && horizontalPageLayout.anchoredPosition.x > xPos ) )
        {
            var difference = Time.deltaTime * speed;
            difference = Mathf.Min( difference, Mathf.Abs( xPos - horizontalPageLayout.anchoredPosition.x ) );
            horizontalPageLayout.anchoredPosition = horizontalPageLayout.anchoredPosition.SetX( horizontalPageLayout.anchoredPosition.x + difference * direction );
            yield return null;
        }

        if( fixPagesAfter )
            ChangePageInstant( horizontalPageLayout.anchoredPosition.x < start_x );
        else
            horizontalPageLayout.anchoredPosition = horizontalPageLayout.anchoredPosition.SetX( xPos );
    }

    void ChangePageInstant( bool left )
    {
        currentPage = ( currentPage + ( left ? 1 : ( pages.Count - 1 ) ) ) % pages.Count;

        foreach( var page in pages )
        {
            if( page == pages[currentPage] )
                continue;

            if( page.gameObject.TryGetComponent( out IBasePage pageHandler ) )
                pageHandler.OnHidden();

            foreach( Transform child in page.transform )
                if( child.TryGetComponent( out IBasePage childHandler ) )
                    childHandler.OnHidden();
        }

        var layoutRoot = horizontalPageLayout.GetChild( 0 );
        var fromIndex = left ? 0 : layoutRoot.childCount - 1;
        var toIndex = ( layoutRoot.childCount - 1 ) - fromIndex;
        layoutRoot.GetChild( fromIndex ).SetSiblingIndex( toIndex );
        horizontalPageLayout.anchoredPosition = horizontalPageLayout.anchoredPosition.SetX( 0.0f );
        blurPage.material.SetFloat( "_Size", 0.0f );

        if( pages[currentPage].gameObject.TryGetComponent( out IBasePage handler ) )
            handler.OnShown();

        foreach( Transform child in pages[currentPage].transform )
            if( child.TryGetComponent( out IBasePage childHandler ) )
                childHandler.OnShown();
    }
}
