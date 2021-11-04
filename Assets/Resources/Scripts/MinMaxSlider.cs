using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

class MinMaxSlider : MonoBehaviour
{
    [SerializeField] private Slider sliderMin = null;
    [SerializeField] private Slider sliderMax = null;
    [SerializeField] RectTransform fill = null;
    [SerializeField] RectTransform minHandle = null;
    [SerializeField] CanvasGroup minHandlePin = null;
    [SerializeField] RectTransform maxHandle = null;
    [SerializeField] CanvasGroup maxHandlePin = null;
    [SerializeField] float interpTime = 0.5f;

    public event Action< float, float > OnValueSet;
    private float cachedMin;
    private float cachedMax;

    private Text minText;
    private Text maxText;
    private readonly Coroutine[] interpCoroutine = new Coroutine[2];

    public float GetMinValue()
    {
        return cachedMin;
    }

    public float GetMaxValue()
    {
        return cachedMax;
    }

    public void SetMinValue( float v )
    {
        sliderMin.value = v;
        cachedMin = sliderMin.value;
    }

    public void SetMaxValue( float v )
    {
        sliderMax.value = v;
        cachedMax = sliderMax.value;
    }

    private void Awake()
    {
        minText = minHandle.GetComponentInChildren<Text>();
        maxText = maxHandle.GetComponentInChildren<Text>();

        sliderMax.minValue = sliderMin.minValue;
        sliderMax.maxValue = sliderMin.maxValue;
        cachedMin = sliderMin.value;
        cachedMax = sliderMax.value;

        sliderMin.onValueChanged.AddListener( ( float value ) =>
        {
            sliderMin.SetValueWithoutNotify( Mathf.Min( value, sliderMax.value ) );
            OnValueChanged();
            FixOrdering();
        } );

        sliderMax.onValueChanged.AddListener( ( float value ) =>
        {
            sliderMax.SetValueWithoutNotify( Mathf.Max( value, sliderMin.value ) );
            OnValueChanged();
            FixOrdering();
        } );

        var minEventDispatcher = sliderMin.GetComponent<EventDispatcher>();
        var maxEventDispatcher = sliderMax.GetComponent<EventDispatcher>();

        OnValueChanged();

        minEventDispatcher.OnBeginDragEvent += ( _ ) =>
        {
            minHandlePin.SetVisibility( true );
        };

        minEventDispatcher.OnEndDragEvent += ( _ ) =>
        {
            if( interpCoroutine[0] != null )
                StopCoroutine( interpCoroutine[0] );
            var targetValue = Mathf.Min( sliderMax.value, Mathf.Round( sliderMin.value ) );
            interpCoroutine[0] = StartCoroutine( InterpHandleToPosition( sliderMin, targetValue ) );
            minHandlePin.SetVisibility( false );

            if( targetValue != cachedMin )
            {
                cachedMin = targetValue;
                OnValueSet?.Invoke( targetValue, GetMaxValue() );
            }
        };

        maxEventDispatcher.OnBeginDragEvent += ( _ ) =>
        {
            maxHandlePin.SetVisibility( true );
        };

        maxEventDispatcher.OnEndDragEvent += ( _ ) =>
        {
            if( interpCoroutine[1] != null )
                StopCoroutine( interpCoroutine[1] );
            var targetValue = Mathf.Max( sliderMin.value, Mathf.Round( sliderMax.value ) );
            interpCoroutine[1] = StartCoroutine( InterpHandleToPosition( sliderMax, targetValue ) );
            maxHandlePin.SetVisibility( false );

            if( targetValue != cachedMax )
            {
                cachedMax = targetValue;
                OnValueSet?.Invoke( GetMinValue(), targetValue );
            }
        };

        //maxEventDispatcher.OnPointerDownEvent += ( eventData ) =>
        //{
        //    if( Mathf.Abs( eventData.position.x - ( minHandle.transform as RectTransform ).position.x ) < Mathf.Abs( eventData.position.x - ( maxHandle.transform as RectTransform ).position.x ) )
        //        sliderMin.OnPointerDown( eventData );
        //    else
        //        sliderMax.OnPointerDown( eventData );
        //};

        minHandlePin.SetVisibility( false );
        maxHandlePin.SetVisibility( false );
    }

    private IEnumerator InterpHandleToPosition( Slider slider, float targetValue )
    {
        var direction = slider.value < targetValue ? 1.0f : -1.0f;
        var speed = Mathf.Abs( targetValue - slider.value ).SafeDivide( interpTime );

        while( ( ( direction > 0.0f && slider.value < targetValue ) ||
                 ( direction < 0.0f && slider.value > targetValue ) ) &&
                 speed > 0.0f )
        {
            slider.value += Time.deltaTime * speed * direction;
            yield return null;
        }

        slider.value = targetValue;
    }

    private void FixOrdering()
    {
        if( sliderMin.value >= sliderMin.maxValue - 0.001f )
            sliderMin.transform.SetAsLastSibling();
        else if( sliderMax.value <= sliderMax.minValue + 0.001f )
            sliderMax.transform.SetAsLastSibling();
    }

    private void OnValueChanged()
    {
        if( fill != null )
        {
            var previousPosition = fill.localPosition;
            fill.anchorMin = minHandle.anchorMin;
            fill.anchorMax = maxHandle.anchorMax;
            fill.offsetMin = new Vector2( minHandle.anchoredPosition.x, 0.0f );
            fill.offsetMax = new Vector2( maxHandle.anchoredPosition.x, 0.0f );

            foreach( Transform child in fill )
                child.localPosition -= ( fill.localPosition - previousPosition );
        }

        if( minText != null )
            minText.text = Mathf.RoundToInt( sliderMin.value ).ToString();
        if( maxText != null )
            maxText.text = Mathf.RoundToInt( sliderMax.value ).ToString();
    }
}
