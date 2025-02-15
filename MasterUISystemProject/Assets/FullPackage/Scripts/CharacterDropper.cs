﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CharacterDropper : MonoBehaviour {

    /// <summary>
    /// GENERAL VARS
    /// </summary>
    public GameObject avatar;
    private Camera mouseCam;
    public bool hasRaycastLock = false;
    
    
    /// <summary>
    /// NEW CHAR DROP VARS
    /// </summary>
    public Toggle randomToggle;
    public Text modelLabel;
    public RectTransform modelList;
    public ToggleGroup modelToggleGroup;
    public RectTransform dropCharacterSelectPanel;
    public Dropdown newCharWanderSelect;
    public Image buttonImage;
    private List<GameObject> loadedCharacters = new List<GameObject>();
    private List<Toggle> toggleList = new List<Toggle>();
    private GameObject charToDrop;
    private bool modelOptionsGreyed;
    private Vector3 dropLocation = Vector3.zero;
    private int randomCharIndex = -1;

    /// <summary>
    /// CHAR INFO VARS
    /// </summary>
    public RectTransform charInfoPanel;
    public Text modelNameLabel;
    public Text wanderModeLabel;
    public Text wanderRangeLabel;
    public Dropdown destinationDropDown;

    /// <summary>
    /// CHAR EDIT VARS
    /// </summary>
    public RectTransform charEditPanel;
    public Projector radiusProjector;
    public InputField radiusInput;
    public ToggleGroup charEditWanderToggleGroup;
    public Image radiusSelectMask;
    public NavMeshWander.WanderMode selectedMode;

    private GameObject charToEdit;
    private NavMeshWander navMeshWanderToEdit;

    /// <summary>
    /// STATES
    /// </summary>
    private bool newCharDrop = false;
    private bool charInfoOpen = false;
    private bool charEditOpen = false;
    private bool charRadiusSelect = false;
    private bool userSetRadius = false;


    void Start()
    {

        ResetMenu();
        if (avatar == null)
        {
            Debug.Log("CharacterDropper.Avatar was null! Searching for tag: Player");
            if (GameObject.FindGameObjectWithTag("Player") != null)
            {
                Debug.Log("Found object tagged: Player");
                avatar = GameObject.FindGameObjectWithTag("Player");
            }
            else
            {
                Debug.Log("CharacterDropper.Avatar was null and no object tagged: Player");
            }
        }

        Object[] tmpArray = Resources.LoadAll("Characters/");
        foreach (Object obj in tmpArray)
        {
            loadedCharacters.Add(obj as GameObject);
            GameObject newToggle = Instantiate(Resources.Load("CustomToggle"), Vector3.zero, Quaternion.identity) as GameObject;
            newToggle.name = obj.name;
            newToggle.transform.SetParent(modelList.transform);
            newToggle.transform.SetAsLastSibling();
            newToggle.transform.FindChild("Label").GetComponent<Text>().text = obj.name;
            newToggle.GetComponent<Toggle>().group = modelToggleGroup;
            modelToggleGroup.RegisterToggle(newToggle.GetComponent<Toggle>());
        }
    }

    void Update()
    {
        // this finds the camera whose viewport contains the mouse cursor
        mouseCam = FindMouseCamera();

        // if the char edit group is set to patrol, disable the mask
        if ( charEditOpen  && charEditWanderToggleGroup.ActiveToggles().ToArray().Count() != 0 
            && charEditWanderToggleGroup.ActiveToggles().ToArray()[0].name == "Patrol")
        {
            radiusProjector.transform.position = navMeshWanderToEdit.localWanderCenter + new Vector3(0, 2, 0);
            if (userSetRadius)
                radiusProjector.orthographicSize = float.Parse(radiusInput.text);
            else
                radiusProjector.orthographicSize = navMeshWanderToEdit.localWanderRadius;

            radiusSelectMask.enabled = false;
        }
        else if (charEditOpen)
        {

            radiusProjector.orthographicSize = 0;

            radiusSelectMask.enabled = true;
        }

        //manage raycast lock
        if (charRadiusSelect || newCharDrop || charInfoOpen)
        {
            if (!hasRaycastLock && RaycastLock.GetLock())
                hasRaycastLock = true;
        }
        else if (hasRaycastLock)
        {
            RaycastLock.GiveLock();
            hasRaycastLock = false;
        }


        if (charRadiusSelect || charInfoOpen)
        {
            radiusProjector.gameObject.SetActive(true);
            radiusProjector.transform.position = navMeshWanderToEdit.localWanderCenter + new Vector3(0, 2, 0);
        }
        else
        {

            radiusProjector.gameObject.SetActive(false);
        }
        //radius select mode
        if (charRadiusSelect)
        {

            if (!userSetRadius)
            {
                //set size of the projector to the distance between the character being editted and the raycast hit at the mouse location
                if (mouseCam != null && hasRaycastLock)
                {
                    RaycastLock.Raycast(mouseCam.ScreenPointToRay(Input.mousePosition), ~(1 << 9 | 1 << 8));
                    radiusProjector.orthographicSize = (charToEdit.transform.position - RaycastLock.hit.point).magnitude;
                }

                else
                    radiusProjector.orthographicSize = navMeshWanderToEdit.localWanderRadius;
            }

            radiusInput.text = radiusProjector.orthographicSize.ToString("F2");

            if (Input.GetMouseButtonDown(0))
            {
                StopCharRadiusSelect(!charEditOpen);
            }

        }
        // if we are dropping a new character
        else if (newCharDrop)
        {
            #region dropping new char
            #region toggles
            if (randomToggle.isOn)
            {
                if (modelToggleGroup.AnyTogglesOn())
                {
                    modelToggleGroup.SetAllTogglesOff();
                }

                if (!modelOptionsGreyed)
                {
                    foreach (Toggle tog in modelToggleGroup.GetComponentsInChildren<Toggle>())
                    {
                        tog.GetComponentInChildren<Text>().color = Color.grey;
                    }
                    modelOptionsGreyed = true;
                }
            }
            else
            {
                if (modelOptionsGreyed)
                {
                    foreach (Toggle tog in modelToggleGroup.GetComponentsInChildren<Toggle>())
                    {
                        tog.GetComponentInChildren<Text>().color = Color.black;
                    }
                    modelOptionsGreyed = false;
                }
            }

            // this if is to make sure that a toggle in the toggle group is on if the random toggle is off
            if (!modelToggleGroup.AnyTogglesOn() && !randomToggle.isOn)
            {
                modelToggleGroup.GetComponentInChildren<Toggle>().isOn = true;
                modelToggleGroup.NotifyToggleOn(modelToggleGroup.GetComponentInChildren<Toggle>());
            }
            #endregion

            //raycast that ignores characters in the scene
            if (mouseCam != null && hasRaycastLock)
                RaycastLock.Raycast(mouseCam.ScreenPointToRay(Input.mousePosition), ~(1 << 9 | 1 << 8));

            #region makes sure the displayed char is correct
            if (charToDrop == null)
                charToDrop = GetCharacter();

            if (modelToggleGroup.ActiveToggles().Count() > 0
                && charToDrop.name != modelToggleGroup.ActiveToggles().ToList()[0].name + "(Clone)"
                && !randomToggle.isOn)
            {
                Destroy(charToDrop);
                charToDrop = GetCharacter();
            }
            #endregion

            //sets the position of the temp avatar
            if (mouseCam != null)
            {
                if (RaycastLock.hit.point != null)
                    dropLocation = RaycastLock.hit.point;
                else
                    dropLocation = avatar.transform.position + avatar.transform.forward * 2f;

                //Debug.Log("drop location: " + dropLocation + " haslock: " + hasRaycastLock);
            }

            if (charToDrop != null)
                charToDrop.transform.position = dropLocation;

            //if we right click at a valid location, drop the character
            if (Input.GetMouseButtonUp(1) && RaycastLock.hit.transform != null)
                DropCharacter();

            #endregion
        }
        else // this is when we are not dropping a new character
        {
            //raycast that ignores the user avatar
            if (mouseCam != null && Input.GetMouseButtonDown(0))
            {
                if (!hasRaycastLock && RaycastLock.GetLock())
                {
                    hasRaycastLock = true;
                }

                if (hasRaycastLock)
                {
                    RaycastLock.Raycast(mouseCam.ScreenPointToRay(Input.mousePosition), ~(1 << 9));
                    //if we are pointing at an existing avatar and left click, open char info
                    if (RaycastLock.hit.transform != null && RaycastLock.hit.transform.GetComponent<NavMeshWander>() != null && !charInfoOpen)
                        OpenCharacterInfo();
                    else
                    {
                        RaycastLock.GiveLock();
                        hasRaycastLock = false;
                    }
                }
            }
            
        }


    }


    void OnDisable()
    {
        ResetMenu();
    }

    void OnEnable()
    {
        ResetMenu();
    }

    public void ResetMenu()
    {
        Destroy(charToDrop);
        newCharDrop = false;
        charInfoOpen = false;
        charEditOpen = false;
        charRadiusSelect = false;
        modelOptionsGreyed = false;
        StopCharRadiusSelect(false);
        CloseCharacterDrop();
        CloseCharacterInfo();
        CloseCharacterEdit();
    }

    public void ToggleMenu()
    {
        newCharDrop = !newCharDrop;
        if (newCharDrop)
        {
            OpenCharacterDrop();
        }
        else
        {
            CloseCharacterDrop();
        }
    }

    public void SetWanderRadiusFromInput()
    {
        userSetRadius = true;
        radiusProjector.orthographicSize = float.Parse(radiusInput.text);
    }

    public void DropCharacter()
    {
        charToDrop.GetComponent<CapsuleCollider>().enabled = true;
        charToDrop.GetComponent<NavMeshAgent>().enabled = true;
        charToDrop.GetComponent<NavMeshWander>().enabled = true;
        charToDrop.GetComponent<NavMeshWander>().mode = (NavMeshWander.WanderMode)newCharWanderSelect.value;

        if ((NavMeshWander.WanderMode)newCharWanderSelect.value == NavMeshWander.WanderMode.Patrol)
        {
            charToEdit = charToDrop;
            navMeshWanderToEdit = charToEdit.GetComponent<NavMeshWander>();
            StartCharRadiusSelect();
        }
        charToDrop = null;
    }

    public void OpenCharacterDrop()
    {
        dropCharacterSelectPanel.gameObject.SetActive(true);
        modelToggleGroup.SetAllTogglesOff();
        randomToggle.isOn = true;
        charToDrop = GetCharacter();
        buttonImage.color = Color.red;
    }

    public void CloseCharacterDrop()
    {
        dropCharacterSelectPanel.gameObject.SetActive(false);
        Destroy(charToDrop);
        charToDrop = null;
        buttonImage.color = Color.white;
        CloseCharacterInfo();
        CloseCharacterEdit();
    }


    private GameObject GetCharacter()
    {
        GameObject returnChar = null;
        if (randomToggle.isOn)
            returnChar = CreateRandomChar();
        else
        {
            toggleList = modelToggleGroup.ActiveToggles().ToList<Toggle>();
            foreach (GameObject character in loadedCharacters)
            {
                if (character.name == toggleList[0].name)
                {
                    modelLabel.text = "Model: " + character.name;
                    returnChar = Instantiate(character, dropLocation, Quaternion.identity) as GameObject;
                }
            }
        }

        returnChar.GetComponent<CapsuleCollider>().enabled = false;
        returnChar.GetComponent<NavMeshAgent>().enabled = false;
        returnChar.GetComponent<NavMeshWander>().enabled = false;
        return returnChar;
    }

    private GameObject CreateRandomChar()
    {
        /*
        int randomIndex = (int)Random.Range(0, loadedCharacters.Count - 1);
        modelLabel.text = "Model: " + loadedCharacters[randomIndex].name;
        */
        randomCharIndex++;
        if (randomCharIndex >= loadedCharacters.Count)
            randomCharIndex = 0;
        modelLabel.text = "Model: " + loadedCharacters[randomCharIndex].name;
        return Instantiate(loadedCharacters[randomCharIndex], dropLocation, Quaternion.identity) as GameObject;
    }


    public void DeleteCharacter()
    {
        Destroy(charToEdit);
        charToEdit = null;
        CloseCharacterInfo();
    }

    private Camera FindMouseCamera()
    {
        List<Camera> camList = (from cam in GameObject.FindObjectsOfType<Camera>() where cam.targetTexture == null select cam).ToList();
        foreach (Camera cam in camList)
        {
            if (Input.mousePosition.x > cam.pixelRect.xMin && Input.mousePosition.x < cam.pixelRect.xMax
                && Input.mousePosition.y > cam.pixelRect.yMin && Input.mousePosition.y < cam.pixelRect.yMax)
            {
                return cam;
            }
        }
        return null;
    }

    public void StartCharRadiusSelect()
    {
        if (charToEdit == null && charToDrop != null)
        {
            charToEdit = charToDrop;
           
            charToDrop = null;
        }
        if(navMeshWanderToEdit == null)
            navMeshWanderToEdit = charToEdit.GetComponent<NavMeshWander>();

        if (charToEdit != null)
            navMeshWanderToEdit.localWanderCenter = charToEdit.transform.position;

        charRadiusSelect = true;
        
    }

    public void StopCharRadiusSelect(bool startMotion)
    {
        if (navMeshWanderToEdit != null)
        {
            navMeshWanderToEdit.localWanderRadius = radiusProjector.orthographicSize;
            if(startMotion)
                navMeshWanderToEdit.ConfigureDestination();
        }
        

        //charToEdit = null;
        //navMeshWanderToEdit = null;
        charRadiusSelect = false;
        
    }

    public void OpenCharacterEdit()
    {
        charEditOpen = true;
        charEditPanel.gameObject.SetActive(true);
        charEditWanderToggleGroup.SetAllTogglesOff();
        radiusInput.text = navMeshWanderToEdit.localWanderRadius.ToString();
        navMeshWanderToEdit.localWanderCenter = charToEdit.transform.position;
        Toggle toggleToActivate = charEditWanderToggleGroup.transform.GetChild((int)navMeshWanderToEdit.mode).GetComponent<Toggle>();
        toggleToActivate.isOn = true;
        charEditWanderToggleGroup.NotifyToggleOn(toggleToActivate);
    }

    public void CloseCharacterEdit()
    {
        if (navMeshWanderToEdit != null && charEditOpen)
        {
            if (charEditWanderToggleGroup.ActiveToggles().ToArray()[0].name == "Idle")
                selectedMode = (NavMeshWander.WanderMode)0;
            else if (charEditWanderToggleGroup.ActiveToggles().ToArray()[0].name == "Explore")
                selectedMode = (NavMeshWander.WanderMode)2;
            else if (charEditWanderToggleGroup.ActiveToggles().ToArray()[0].name == "Patrol")
            {
                selectedMode = (NavMeshWander.WanderMode)1;
                navMeshWanderToEdit.localWanderCenter = charToEdit.transform.position;
                navMeshWanderToEdit.localWanderRadius = float.Parse(radiusInput.text);

            }
        }

        charEditOpen = false;
        charEditPanel.gameObject.SetActive(false);
    }

    public void OpenCharacterInfo()
    {
        Destroy(charToDrop);
        charInfoOpen = true;
        charRadiusSelect = false;
        charToEdit = RaycastLock.hit.transform.gameObject;
        navMeshWanderToEdit = charToEdit.GetComponent<NavMeshWander>();
        charToEdit.GetComponent<NavMeshAgent>().Stop();
        charToEdit.GetComponent<Animator>().enabled = false;
        charInfoPanel.gameObject.SetActive(true);
        selectedMode = navMeshWanderToEdit.mode;

        if (Input.mousePosition.x > Screen.width - charInfoPanel.sizeDelta.x)
        {
            if (Input.mousePosition.y > Screen.height - charInfoPanel.sizeDelta.y)
            {
                charInfoPanel.transform.position = new Vector3(Screen.width - charInfoPanel.sizeDelta.x, Screen.height - charInfoPanel.sizeDelta.y, 0);
            }
            else if (Input.mousePosition.y < charEditPanel.sizeDelta.y)
            {
                charInfoPanel.transform.position = new Vector3(Screen.width - charInfoPanel.sizeDelta.x, charEditPanel.sizeDelta.y, 0);
            }
            else
                charInfoPanel.transform.position = new Vector3(Screen.width - charInfoPanel.sizeDelta.x, Input.mousePosition.y, 0);
        }
        else if (Input.mousePosition.y > Screen.height - charInfoPanel.sizeDelta.y)
            charInfoPanel.transform.position = new Vector3(Input.mousePosition.x, Screen.height - charInfoPanel.sizeDelta.y, 0);
        else if (Input.mousePosition.y < charEditPanel.sizeDelta.y)
            charInfoPanel.transform.position = new Vector3(Input.mousePosition.x, charEditPanel.sizeDelta.y, 0);
        else
            charInfoPanel.transform.position = Input.mousePosition;

        UpdateCharInfoLabels();

        destinationDropDown.ClearOptions();
        destinationDropDown.AddOptions(new List<string>() { "None" });

        destinationDropDown.AddOptions(new List<string>( POIButtonManager.originalHandler.projectPOIs.Select(e => e.buttonName).ToList()));
    }

    public void CloseCharacterInfo()
    {
        if (hasRaycastLock)
        {
            RaycastLock.GiveLock();
            hasRaycastLock = false;
        }

        CloseCharacterEdit();
        if (charToEdit != null)
            charToEdit.GetComponent<Animator>().enabled = true;
        if (navMeshWanderToEdit != null)
        {
            if(destinationDropDown.value == 0)
                navMeshWanderToEdit.ConfigureDestination();
            else
                navMeshWanderToEdit.ConfigureDestination(destinationDropDown.value - 1);
        }
        ApplyOptions();

        charToEdit = null;
        navMeshWanderToEdit = null;
        charInfoOpen = false;
        charInfoPanel.gameObject.SetActive(false);
        
        
    }

    public void UpdateCharInfoLabels()
    {
        modelNameLabel.text = charToEdit.gameObject.name.Remove(charToEdit.gameObject.name.IndexOf("("));

        wanderModeLabel.text = navMeshWanderToEdit.mode.ToString();

        if ((int)navMeshWanderToEdit.mode == 0)
        {
            radiusProjector.orthographicSize = 0;
            wanderRangeLabel.text = "0";
        }
        else if ((int)navMeshWanderToEdit.mode == 1)
        {
            radiusProjector.orthographicSize = navMeshWanderToEdit.localWanderRadius;
            wanderRangeLabel.text = navMeshWanderToEdit.localWanderRadius.ToString();
        }
        else if ((int)navMeshWanderToEdit.mode == 2)
        {
            radiusProjector.orthographicSize = 0;
            wanderRangeLabel.text = "Infinite";
        }

        destinationDropDown.value = navMeshWanderToEdit.poiDestination;
    }

    public void ApplyOptions()
    {
        if (charToEdit != null)
        {
            navMeshWanderToEdit.mode = selectedMode;


            UpdateCharInfoLabels();
        }
    }

}


/// old code from Update
/// 
/*

//if we are in radius select mode, set the size of the projector
if (radiusSelectMode)
{
    if ( mouseCam != null && Physics.Raycast(mouseCam.ScreenPointToRay(Input.mousePosition), out hit, 1000, ~(3 << 8)))
        radiusProjector.orthographicSize = (charToDrop.transform.position - hit.point).magnitude;
}


//if we right click while hover over something that isnt an existing avatar
if (Input.GetMouseButtonDown(1) && hit.transform != null && hit.transform.GetComponent<NavMeshWander>() == null)
{
    if (!radiusSelectMode)
    {
        if ((NavMeshWander.WanderMode)newCharWanderSelect.value == NavMeshWander.WanderMode.Local)
        {
            Debug.Log("dropping character in local wander");
            charToDrop.GetComponent<NavMeshWander>().localWanderCenter = hit.point;
            charToDrop.GetComponent<NavMeshWander>().enabled = true;
            radiusProjector.gameObject.SetActive(true);
            radiusProjector.transform.position = charToDrop.transform.position + new Vector3(0, 2, 0);
            radiusSelectMode = true;
        }
        else
        {
            charToDrop.GetComponent<CapsuleCollider>().enabled = true;
            charToDrop.GetComponent<NavMeshAgent>().enabled = true;
            charToDrop.GetComponent<NavMeshWander>().enabled = true;
            charToDrop.GetComponent<NavMeshWander>().mode = (NavMeshWander.WanderMode)newCharWanderSelect.value;
            charToDrop = GetCharacter();
        }
    }
    else
    {

        charToDrop.GetComponent<CapsuleCollider>().enabled = true;
        charToDrop.GetComponent<NavMeshAgent>().enabled = true;
        charToDrop.GetComponent<NavMeshWander>().enabled = true;
        charToDrop.GetComponent<NavMeshWander>().localWanderRadius = radiusProjector.orthographicSize;
        charToDrop.GetComponent<NavMeshWander>().mode = (NavMeshWander.WanderMode)newCharWanderSelect.value;
        radiusProjector.gameObject.SetActive(false);
        radiusSelectMode = false;
        charToDrop = GetCharacter();
    }
}

//drop the temp avatar
if (Input.GetMouseButtonDown(2))
{
    Destroy(charToDrop);
    charToDrop = GetCharacter();
}
}// chareditmode
}
else // not in drop mode
{
// GENERAL RAYCAST INTO THE VIRTUAL WORLD
if (mouseCam != null)
Physics.Raycast(mouseCam.ScreenPointToRay(Input.mousePosition), out hit, 1000, ~(1 << 9));

//if we are in radius select mode, set the size of the projector
if (radiusSelectMode)
{
if (mouseCam != null && Physics.Raycast(mouseCam.ScreenPointToRay(Input.mousePosition), out hit, 1000, ~(3 << 8)))
    radiusProjector.orthographicSize = (charToEdit.transform.position - hit.point).magnitude;
}

//if we are pointing at an existing avatar
if (hit.transform != null && hit.transform.GetComponent<NavMeshWander>() != null)
if (Input.GetMouseButtonDown(0) && !charEditModeOn)
    OpenCharacterInfo();

if (charEditModeOn)
{

if ((NavMeshWander.WanderMode)charEditWanderSelect.value == NavMeshWander.WanderMode.Local && !radiusSelectMode && !setCharLocalRadius)
{
    Debug.Log("char edit wander is local");
    radiusProjector.gameObject.SetActive(true);
    radiusProjector.transform.position = charToEdit.transform.position + new Vector3(0, 2, 0);
    radiusSelectMode = true;
}
}

if (radiusSelectMode && Input.GetMouseButtonUp(1))
{
radiusSelectMode = false;
setCharLocalRadius = true;
radiusProjector.gameObject.SetActive(false);
}
}
*/
