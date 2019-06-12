using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToastController : MonoBehaviour {
    private Coroutine _currentCoroutine;
    private Text _text;

    void Start() {
        _text = transform.GetChild(0).GetComponent<Text>();
        _text.gameObject.SetActive(false);
    }

    public void DisplayMessage(string message, float durationInSeconds) {
        if (_currentCoroutine != null) {
            StopCoroutine(_currentCoroutine);
        }
        _currentCoroutine = StartCoroutine(DisplayToast(message, durationInSeconds));
    }

    private IEnumerator DisplayToast(string message, float durationInSeconds) {
        _text.gameObject.SetActive(true);
        _text.text = message;

        yield return new WaitForSeconds(durationInSeconds);

        _text.text = "";
        _text.gameObject.SetActive(false);
    }
}
