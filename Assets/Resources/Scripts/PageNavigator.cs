using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PageNavigator : MonoBehaviour
{
    [SerializeField] private List<GameObject> pages = new List<GameObject>();
    [SerializeField] private RectTransform panel = null;
    [SerializeField] private float centreX = 0.0f;
    [SerializeField] private float moveTimeSec = 0.0f;
    [SerializeField] private GameObject confirmDeleteDataPanel = null;
    private float leftX;
    private int currentPage = 0;

    void Start()
    {
        gameObject.SetActive( false );
        confirmDeleteDataPanel.SetActive( false );

        foreach( var page in pages )
            page.SetActive( false );

        leftX = panel.anchoredPosition.x;
        Utility.FunctionTimer.CreateTimer( 0.01f, () => ShowPage( 0 ) );
    }

    public void ToggleMenu()
    {
        if( !gameObject.activeSelf )
            gameObject.SetActive( true );
        confirmDeleteDataPanel.SetActive( false );

        StartCoroutine( MovePanel( panel.anchoredPosition.x < centreX ? centreX : leftX ) );
    }

    private IEnumerator MovePanel( float xPos )
    {
        float speed = Mathf.Abs( xPos - panel.anchoredPosition.x ) / moveTimeSec;
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

        if( gameObject.activeSelf && direction == -1 )
            gameObject.SetActive( false );
    }

    public void ShowPage( int index )
    {
        foreach( var page in pages )
        {
            if( page == pages[index] )
                continue;

            page.SetActive( false );

            if( page.gameObject.TryGetComponent( out IBasePage pageHandler ) )
                pageHandler.OnHidden();

            foreach( Transform child in page.transform )
                if( child.TryGetComponent( out IBasePage childHandler ) )
                    childHandler.OnHidden();
        }

        pages[index].SetActive( true );
        gameObject.SetActive( false );

        if( pages[index].gameObject.TryGetComponent( out IBasePage handler ) )
            handler.OnShown();

        foreach( Transform child in pages[index].transform )
            if( child.TryGetComponent( out IBasePage childHandler ) )
                childHandler.OnShown();

        panel.anchoredPosition = panel.anchoredPosition.SetX( leftX );
        currentPage = index;
    }

    public void ToggleActive( GameObject obj )
    {
        obj.ToggleActive();
    }
}
