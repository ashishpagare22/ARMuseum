using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[Serializable]
public class ArtworkRecord
{
    public string imageName;
    public string title;
    public string artist;
    public string year;
    public string medium;
    [TextArea(2, 5)] public string description;
}

public class MuseumImageTracker : MonoBehaviour
{
    [SerializeField] private ARTrackedImageManager imageManager;
    [SerializeField] private GameObject informationPanel;
    [SerializeField] private TMPro.TMP_Text statusText;
    [SerializeField] private TMPro.TMP_Text titleText;
    [SerializeField] private TMPro.TMP_Text detailsText;
    [SerializeField] private ArtworkRecord[] artworks = Array.Empty<ArtworkRecord>();

    private ARTrackedImage dismissedImage;
    private string displayedImageName;

    private void Awake()
    {
        EnsureTouchInput();
        EnsureCloseButton();
        ShowScanningMessage();
    }

    private void OnEnable()
    {
        if (imageManager != null)
            imageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
    }

    private void OnDisable()
    {
        if (imageManager != null)
            imageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
    }

    private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
    {
        foreach (var image in changes.added)
            TryShow(image);

        foreach (var image in changes.updated)
        {
            if (image.trackingState == TrackingState.Tracking &&
                (dismissedImage == null || image.trackableId != dismissedImage.trackableId))
                TryShow(image);
        }

        if (changes.removed.Count > 0 && imageManager.trackables.count == 0)
        {
            dismissedImage = null;
            displayedImageName = null;
            ShowScanningMessage();
        }
    }

    private void TryShow(ARTrackedImage image)
    {
        if (image == null || image.trackingState != TrackingState.Tracking)
            return;

        // Keep a dismissed result closed while its image remains in view. A different
        // reference image is allowed to replace it immediately.
        if (dismissedImage != null && image.trackableId == dismissedImage.trackableId)
            return;

        displayedImageName = image.referenceImage.name;
        var record = Array.Find(artworks, item => item != null &&
            string.Equals(item.imageName, displayedImageName, StringComparison.OrdinalIgnoreCase));

        if (informationPanel != null)
            informationPanel.SetActive(true);
        if (statusText != null)
            statusText.text = "PAINTING RECOGNISED";
        if (titleText != null)
            titleText.text = record != null && !string.IsNullOrWhiteSpace(record.title)
                ? record.title : displayedImageName;
        if (detailsText != null)
        {
            if (record == null)
                detailsText.text = "Add this painting's artist, year, medium and description in the Artwork Records list.";
            else
                detailsText.text = $"{record.artist}\n{record.year}  ·  {record.medium}\n\n{record.description}";
        }
    }

    public void DismissCurrentArtwork()
    {
        if (imageManager != null && !string.IsNullOrEmpty(displayedImageName))
        {
            foreach (var image in imageManager.trackables)
            {
                if (string.Equals(image.referenceImage.name, displayedImageName, StringComparison.OrdinalIgnoreCase))
                {
                    dismissedImage = image;
                    break;
                }
            }
        }

        if (informationPanel != null)
            informationPanel.SetActive(false);
    }

    private void EnsureTouchInput()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private void EnsureCloseButton()
    {
        if (informationPanel == null || informationPanel.transform.Find("Close Button") != null)
            return;

        if (statusText != null)
            statusText.rectTransform.anchorMax = new Vector2(0.70f, statusText.rectTransform.anchorMax.y);
        if (titleText != null)
            titleText.rectTransform.anchorMax = new Vector2(0.76f, titleText.rectTransform.anchorMax.y);

        var buttonObject = new GameObject("Close Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(informationPanel.transform, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.79f, 0.82f);
        rect.anchorMax = new Vector2(0.94f, 0.96f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.22f, 0.24f, 0.26f, 1f);
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(DismissCurrentArtwork);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        label.text = "CLOSE";
        label.fontSize = 18;
        label.fontStyle = TMPro.FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        if (titleText != null)
            label.font = titleText.font;
    }

    private void ShowScanningMessage()
    {
        if (informationPanel != null)
            informationPanel.SetActive(true);
        if (statusText != null)
            statusText.text = "NATIONAL GALLERY · AR GUIDE";
        if (titleText != null)
            titleText.text = "Scan a painting";
        if (detailsText != null)
            detailsText.text = "Point your camera at one of the paintings in the guide. Its artwork details will appear here.";
    }
}
