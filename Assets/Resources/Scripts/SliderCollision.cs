using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SliderCollision : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Slider minSlider = null;
    [SerializeField] Slider maxSlider = null;
    [SerializeField] RectTransform minHandle = null;
    [SerializeField] RectTransform maxHandle = null;

    GameObject newTarget;
    bool min;

    public void OnPointerDown( PointerEventData eventData )
    {
        if( Mathf.Abs( eventData.position.x - ( minHandle.transform as RectTransform ).position.x ) < Mathf.Abs( eventData.position.x - ( maxHandle.transform as RectTransform ).position.x ) )
        {
            ExecuteEvents.Execute( minSlider.gameObject, eventData, ExecuteEvents.pointerDownHandler );
            min = true;
        }
        else
        {
            ExecuteEvents.Execute( maxSlider.gameObject, eventData, ExecuteEvents.pointerDownHandler );
            min = false;
        }

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll( eventData, raycastResults );

        foreach( var x in raycastResults )
        {
            if( x.gameObject == minHandle.gameObject || x.gameObject == maxHandle.gameObject )
            {
                newTarget = x.gameObject;
                ExecuteEvents.Execute( ( min ? minSlider : maxSlider ).gameObject, eventData, ExecuteEvents.pointerDownHandler );
                break;
            }
        }
    }

    public void OnPointerUp( PointerEventData eventData )
    {
        ExecuteEvents.Execute( newTarget, eventData, ExecuteEvents.pointerUpHandler );
        ExecuteEvents.Execute( ( min ? minSlider : maxSlider ).gameObject, eventData, ExecuteEvents.pointerUpHandler );
    }
    
    public void OnBeginDrag( PointerEventData eventData )
    {
        ExecuteEvents.Execute( newTarget, eventData, ExecuteEvents.beginDragHandler );
        ExecuteEvents.Execute( ( min ? minSlider : maxSlider ).gameObject, eventData, ExecuteEvents.beginDragHandler );
    }

    public void OnEndDrag( PointerEventData eventData )
    {
        ExecuteEvents.Execute( newTarget, eventData, ExecuteEvents.endDragHandler );
        ExecuteEvents.Execute( ( min ? minSlider : maxSlider ).gameObject, eventData, ExecuteEvents.endDragHandler );
    }

    public void OnDrag( PointerEventData eventData )
    {
        ExecuteEvents.Execute( newTarget, eventData, ExecuteEvents.dragHandler );
        ExecuteEvents.Execute( ( min ? minSlider : maxSlider ).gameObject, eventData, ExecuteEvents.dragHandler );
    }
}
