using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using System;

public class SteamItem {
	public enum ItemInstallType {
		NotInstalled = 0,
		NotPresentOnDisk,
		Installing,
		Installed
	}

	public ulong FolderSize;
	public string FolderPath;
	public ItemInstallType InstallType;
	public uint Timestamp;
	public PublishedFileId_t PublishedFIleId;

	public ulong bytesDownloaded;
	public ulong totalDownloadBytes;
}

public class SteamDownloadBugTest : MonoBehaviour {
	protected Callback<DownloadItemResult_t> downloadItemResultHandle;

	private const int MaxEntries = 20;
	private List<SteamItem> subscribedItems = new List<SteamItem>();
	private bool isDirty;
	private bool isDownloading;
	private float lastUpdateTime;
	private float updateDelta;
	private float lastTimeJumpLength;

	#region MonoBehaviour

	private void Awake() {
		if (!SteamManager.Initialized) {
			Debug.Log("Steam Manager not initialized, destroying myself");
			Destroy(gameObject);
			return;
		}
	}

	private void Start() {
		downloadItemResultHandle = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
		Refresh();
	}

	private void Update() {
		if (!isDownloading) {
			return;
		}

		updateDelta = Time.realtimeSinceStartup - lastUpdateTime;
		lastUpdateTime = Time.realtimeSinceStartup;

		if (updateDelta > 0.5f) {
			lastTimeJumpLength = updateDelta;
			Debug.LogFormat("Game froze for {0} seconds", updateDelta);
		}
	}

	private void OnDisable() {
		if (!SteamManager.Initialized) {
			return;
		}
		downloadItemResultHandle.Dispose();
	}

	#endregion MonoBehaviour

	#region Steam Workshop logic

	private void GetSubscribedItems() {
		uint entries = SteamUGC.GetNumSubscribedItems();
		PublishedFileId_t[] subscribedIds = new PublishedFileId_t[entries];
		SteamUGC.GetSubscribedItems(subscribedIds, entries);

		for (int i = 0; i < Math.Min(entries, MaxEntries); i++) {
			PublishedFileId_t subscribedItemId = subscribedIds[i];
			ProcessSubscribedItem(subscribedItemId);
		}
	}

	private void ProcessSubscribedItem(PublishedFileId_t subscribedItemId) {
		SteamItem item = new SteamItem();
		item.PublishedFIleId = subscribedItemId;

		if (SteamUGC.GetItemInstallInfo(subscribedItemId, out item.FolderSize, out item.FolderPath, 1024, out item.Timestamp)) {
			DetermineInstallType(item);
		}

		subscribedItems.Add(item);
	}

	private void DetermineInstallType(SteamItem item) {
		if (Directory.Exists(item.FolderPath)) {
			item.InstallType = SteamItem.ItemInstallType.Installed;
		} else {
			if (IsItemDownloading(item.PublishedFIleId)) {
				item.InstallType = SteamItem.ItemInstallType.Installing;
			} else {
				item.InstallType = SteamItem.ItemInstallType.NotPresentOnDisk;
			}
		}
		
	}

	private bool IsItemDownloading(PublishedFileId_t publishedFileId) {
		uint itemState = SteamUGC.GetItemState(publishedFileId);
		
		return (itemState & (uint)EItemState.k_EItemStateDownloadPending) != 0
			&& (itemState & (uint)EItemState.k_EItemStateDownloading) != 0;
	}

	private void UpdateInstallInfo(SteamItem item) {
		if (!IsItemDownloading(item.PublishedFIleId)) {
			DetermineInstallType(item);
			return;
		}
		
		if (!SteamUGC.GetItemDownloadInfo(item.PublishedFIleId, out item.bytesDownloaded, out item.totalDownloadBytes)) {
			DetermineInstallType(item);
		} else {
			item.InstallType = SteamItem.ItemInstallType.Installing;
		}
	}

	private void DownloadItemContent(SteamItem item) {
		if (!SteamUGC.DownloadItem(item.PublishedFIleId, true)) {
			Debug.LogWarning("Could not download item...");
			item.InstallType = SteamItem.ItemInstallType.NotInstalled;
		} else {
			Debug.LogFormat("Downloading content for item: {0}", item.PublishedFIleId);
		}
	}

	private void OnDownloadItemResult(DownloadItemResult_t downloadItemResult) {
		isDownloading = false;
		 
		if (downloadItemResult.m_eResult != EResult.k_EResultOK) {
			Debug.LogErrorFormat("Failed to download item: {0}, error: {1}", downloadItemResult.m_nPublishedFileId, downloadItemResult.m_eResult);
			return;
		}

		Debug.LogFormat("Item downloaded: {0}", downloadItemResult.m_nPublishedFileId);
		Refresh();
	}

	#endregion Steam Workshop logic

	#region UI logic

	private void Refresh() {
		isDirty = false;
		Clear();
		GetSubscribedItems();
	}

	private void Clear() {
		subscribedItems.Clear();
	}

	private void OnGUI() {
		GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
		GUILayout.BeginVertical();
		string timeInfo = string.Format("Current time: {0}, time since startup: {1}, lastTimeJumpLength: {2}", Time.time.ToString("0.00"), Time.realtimeSinceStartup.ToString("0.00"), lastTimeJumpLength.ToString("0.00"));
		GUILayout.Label(timeInfo);
		GUILayout.Label("Subscribed Steam items (" + subscribedItems.Count + ")");
		if (GUILayout.Button("Refresh", GUILayout.Width(100))) {
			Refresh();
		}
		GUILayout.Space(20);

		DrawSubscribedItems();

		GUILayout.EndVertical();
		GUILayout.EndArea();

		if (isDirty) {
			Refresh();
		}
	}

	private void DrawSubscribedItems() {
		foreach(SteamItem item in subscribedItems) {
			UpdateInstallInfo(item);
			DrawSubscribedItem(item);
		}
	}

	private void DrawSubscribedItem(SteamItem item) {
		GUILayout.BeginHorizontal(GUILayout.Width(500));
		string itemString = string.Format("[{0}] Folder size: {1}, Install type: {2}", item.PublishedFIleId, item.FolderSize, item.InstallType);
		GUILayout.Label(itemString);
		GUILayout.Space(10);

		if (item.InstallType == SteamItem.ItemInstallType.Installed) {
			if (GUILayout.Button("Delete", GUILayout.Width(80))) {
				Directory.Delete(item.FolderPath, true);
				isDirty = true;
			}
		} else if (item.InstallType == SteamItem.ItemInstallType.Installing) {
			string installString = string.Format("{0} / {1} bytes downloaded...", item.bytesDownloaded, item.totalDownloadBytes);
			GUILayout.Label(installString);
		} else if (item.InstallType == SteamItem.ItemInstallType.NotInstalled || item.InstallType == SteamItem.ItemInstallType.NotPresentOnDisk) {
			if (GUILayout.Button("Download")) {
				lastUpdateTime = Time.realtimeSinceStartup;
				isDownloading = true;
				DownloadItemContent(item);
			}
		}

		GUILayout.EndHorizontal();
	}

	#endregion
}
