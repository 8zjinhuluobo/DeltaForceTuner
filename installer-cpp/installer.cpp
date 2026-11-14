#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif

#include <windows.h>
#include <commctrl.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <shobjidl.h>
#include <tlhelp32.h>
#include <string>
#include <vector>
#include <cstdio>
#include "resource.h"

using std::wstring;
using std::vector;

static HINSTANCE g_inst;
static HWND g_hwnd, g_edit, g_browse, g_desktop, g_launch, g_install, g_cancel, g_progress;
static HWND g_delSettings, g_uninstall, g_uCancel;
static HFONT g_font, g_fontBold, g_fontTitle, g_fontSmall;
static bool g_busy = false;
static bool g_uiTest = false;
static int g_progressValue = 0;
static wstring g_status;
static wstring g_uninstallDir;

static const COLORREF C_BG = RGB(246, 248, 251);
static const COLORREF C_TEXT = RGB(24, 31, 42);
static const COLORREF C_MUTED = RGB(99, 113, 132);
static const COLORREF C_HEADER = RGB(11, 17, 25);
static const COLORREF C_ORANGE = RGB(255, 122, 26);
static const COLORREF C_ORANGE_DARK = RGB(211, 88, 10);
static const COLORREF C_CARD = RGB(236, 241, 247);
static const COLORREF C_BORDER = RGB(207, 217, 228);
static const COLORREF C_RED = RGB(214, 58, 58);

static const wchar_t* APP_NAME = L"三角洲行动 · 本机调优助手";
static const wchar_t* APP_EXE = L"DeltaForceTuner.exe";
static const wchar_t* UNINSTALL_EXE = L"Uninstall.exe";
static const wchar_t* UNINSTALL_KEY = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\DeltaForceTuner";

static wstring GetFolder(int csidl)
{
    wchar_t buf[MAX_PATH];
    if (SHGetFolderPathW(NULL, csidl | CSIDL_FLAG_CREATE, NULL, SHGFP_TYPE_CURRENT, buf) == S_OK)
        return wstring(buf);
    return wstring();
}

static wstring Trim(const wstring& s)
{
    size_t a = 0, b = s.size();
    while (a < b && (s[a] == L' ' || s[a] == L'\t' || s[a] == L'"')) a++;
    while (b > a && (s[b - 1] == L' ' || s[b - 1] == L'\t' || s[b - 1] == L'"')) b--;
    return s.substr(a, b - a);
}

static wstring Join(const wstring& dir, const wstring& name)
{
    if (dir.empty()) return name;
    wchar_t last = dir[dir.size() - 1];
    if (last == L'\\' || last == L'/') return dir + name;
    return dir + L"\\" + name;
}

static bool FileExists(const wstring& p)
{
    DWORD a = GetFileAttributesW(p.c_str());
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

static bool DirExists(const wstring& p)
{
    DWORD a = GetFileAttributesW(p.c_str());
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY);
}

static wstring ExeDir()
{
    wchar_t buf[MAX_PATH];
    GetModuleFileNameW(NULL, buf, MAX_PATH);
    wstring p(buf);
    size_t pos = p.find_last_of(L"\\/");
    if (pos == wstring::npos) return L".";
    return p.substr(0, pos);
}

static wstring DefaultDir()
{
    return Join(Join(GetFolder(CSIDL_LOCAL_APPDATA), L"Programs"), L"DeltaForceTuner");
}

static wstring StartMenuDir()
{
    return Join(Join(Join(GetFolder(CSIDL_APPDATA), L"Microsoft"), L"Windows"), L"Start Menu\\Programs\\DeltaForceTuner");
}

static wstring StartMenuLink()
{
    return Join(StartMenuDir(), L"三角洲行动调优助手.lnk");
}

static wstring DesktopLink()
{
    return Join(GetFolder(CSIDL_DESKTOPDIRECTORY), L"三角洲行动调优助手.lnk");
}

static wstring ConfigDir()
{
    return Join(GetFolder(CSIDL_LOCAL_APPDATA), L"DeltaForceTuner");
}

static wstring GetText(HWND h)
{
    int n = GetWindowTextLengthW(h);
    if (n <= 0) return wstring();
    vector<wchar_t> b(n + 1);
    GetWindowTextW(h, &b[0], n + 1);
    return wstring(&b[0]);
}

static void SetStatus(const wchar_t* s)
{
    g_status = s ? s : L"";
    if (g_hwnd) {
        InvalidateRect(g_hwnd, NULL, FALSE);
        UpdateWindow(g_hwnd);
    }
}

static void SetProgress(int v)
{
    g_progressValue = v;
    if (g_progress) {
        SendMessageW(g_progress, PBM_SETPOS, (WPARAM)v, 0);
        UpdateWindow(g_progress);
    }
}

static void AppendLog(const wstring& s)
{
    wchar_t tmp[MAX_PATH];
    GetTempPathW(MAX_PATH, tmp);
    wstring path = Join(tmp, L"DeltaForceTunerSetup.log");
    HANDLE h = CreateFileW(path.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL,
        OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) return;
    SYSTEMTIME st;
    GetLocalTime(&st);
    wchar_t line[2048];
    swprintf(line, 2048, L"%04d-%02d-%02d %02d:%02d:%02d  %ls\r\n",
        st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, s.c_str());
    DWORD written = 0;
    WriteFile(h, line, (DWORD)(wcslen(line) * sizeof(wchar_t)), &written, NULL);
    CloseHandle(h);
}

static wstring NormalizeDir(const wstring& input, wstring& err)
{
    wstring s = Trim(input);
    if (s.empty()) { err = L"安装目录不能为空。"; return wstring(); }
    wchar_t full[32768];
    if (!GetFullPathNameW(s.c_str(), 32768, full, NULL)) { err = L"安装目录无效。"; return wstring(); }
    wstring dir(full);
    if (PathIsRootW(dir.c_str())) { err = L"请不要安装到磁盘根目录。"; return wstring(); }
    wstring win = GetFolder(CSIDL_WINDOWS);
    if (!win.empty() && _wcsnicmp(dir.c_str(), win.c_str(), win.size()) == 0 &&
        (dir.size() == win.size() || dir[win.size()] == L'\\')) {
        err = L"请不要安装到 Windows 系统目录。";
        return wstring();
    }
    if (DirExists(dir) == false && FileExists(dir)) { err = L"目标路径是一个文件。"; return wstring(); }
    while (dir.size() > 3 && dir[dir.size() - 1] == L'\\') dir.erase(dir.size() - 1);
    return dir;
}

static bool CreateDir(const wstring& dir, wstring& err)
{
    int r = SHCreateDirectoryExW(NULL, dir.c_str(), NULL);
    if (r == ERROR_SUCCESS || r == ERROR_ALREADY_EXISTS) return true;
    err = L"无法创建安装目录（错误 " + std::to_wstring(r) + L"）。";
    return false;
}

static bool ExtractResource(UINT id, const wstring& dest, wstring& err)
{
    HRSRC hr = FindResourceW(g_inst, MAKEINTRESOURCEW(id), RT_RCDATA);
    if (!hr) { err = L"安装包缺少资源。"; return false; }
    DWORD size = SizeofResource(g_inst, hr);
    HGLOBAL hg = LoadResource(g_inst, hr);
    if (!hg) { err = L"无法读取安装资源。"; return false; }
    void* data = LockResource(hg);
    if (!data) { err = L"无法锁定安装资源。"; return false; }
    HANDLE h = CreateFileW(dest.c_str(), GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) { err = L"无法写入：" + dest; return false; }
    DWORD written = 0;
    BOOL ok = WriteFile(h, data, size, &written, NULL);
    CloseHandle(h);
    if (!ok || written != size) { err = L"写入不完整：" + dest; return false; }
    return true;
}

static bool CreateShortcut(const wstring& link, const wstring& target, const wstring& workdir,
    const wstring& icon, const wstring& desc)
{
    IShellLinkW* sl = NULL;
    IPersistFile* pf = NULL;
    HRESULT hr = CoCreateInstance(CLSID_ShellLink, NULL, CLSCTX_INPROC_SERVER, IID_IShellLinkW, (void**)&sl);
    if (FAILED(hr) || !sl) return false;
    sl->SetPath(target.c_str());
    sl->SetWorkingDirectory(workdir.c_str());
    sl->SetArguments(L"");
    sl->SetIconLocation(icon.c_str(), 0);
    sl->SetDescription(desc.c_str());
    hr = sl->QueryInterface(IID_IPersistFile, (void**)&pf);
    if (SUCCEEDED(hr) && pf) hr = pf->Save(link.c_str(), TRUE);
    if (pf) pf->Release();
    sl->Release();
    return SUCCEEDED(hr);
}

static void DeleteShortcuts()
{
    DeleteFileW(StartMenuLink().c_str());
    DeleteFileW(DesktopLink().c_str());
    RemoveDirectoryW(StartMenuDir().c_str());
}

static void SetRegString(HKEY key, const wchar_t* name, const wstring& value)
{
    RegSetValueExW(key, name, 0, REG_SZ, (const BYTE*)value.c_str(),
        (DWORD)((value.size() + 1) * sizeof(wchar_t)));
}

static void SetRegDword(HKEY key, const wchar_t* name, DWORD value)
{
    RegSetValueExW(key, name, 0, REG_DWORD, (const BYTE*)&value, sizeof(value));
}

static bool WriteUninstallEntry(const wstring& dir, wstring& err)
{
    HKEY key = NULL;
    LONG r = RegCreateKeyExW(HKEY_CURRENT_USER, UNINSTALL_KEY, 0, NULL, 0, KEY_WRITE, NULL, &key, NULL);
    if (r != ERROR_SUCCESS) { err = L"无法写入卸载信息（错误 " + std::to_wstring(r) + L"）。"; return false; }
    wstring app = Join(dir, APP_EXE);
    wstring un = Join(dir, UNINSTALL_EXE);
    wchar_t date[16];
    SYSTEMTIME st;
    GetLocalTime(&st);
    swprintf(date, 16, L"%04d%02d%02d", st.wYear, st.wMonth, st.wDay);
    DWORD sizeKb = 1;
    WIN32_FIND_DATAW fd;
    HANDLE hf = FindFirstFileW(Join(dir, L"*").c_str(), &fd);
    if (hf != INVALID_HANDLE_VALUE) {
        ULONGLONG total = 0;
        do {
            if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) total += fd.nFileSizeLow;
        } while (FindNextFileW(hf, &fd));
        FindClose(hf);
        sizeKb = (DWORD)(total / 1024);
        if (sizeKb < 1) sizeKb = 1;
    }
    SetRegString(key, L"DisplayName", APP_NAME);
    SetRegString(key, L"DisplayVersion", L"1.0.1");
    SetRegString(key, L"Publisher", L"Local");
    SetRegString(key, L"InstallLocation", dir);
    SetRegString(key, L"DisplayIcon", app + L",0");
    SetRegString(key, L"UninstallString", L"\"" + un + L"\" --uninstall");
    SetRegString(key, L"QuietUninstallString", L"\"" + un + L"\" --uninstall --quiet");
    SetRegString(key, L"InstallDate", date);
    SetRegDword(key, L"EstimatedSize", sizeKb);
    SetRegDword(key, L"NoModify", 1);
    SetRegDword(key, L"NoRepair", 1);
    RegCloseKey(key);
    return true;
}

static wstring ReadInstallLocation()
{
    HKEY key = NULL;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, UNINSTALL_KEY, 0, KEY_READ, &key) != ERROR_SUCCESS) return wstring();
    wchar_t buf[32768];
    DWORD type = 0, size = sizeof(buf);
    wstring result;
    if (RegQueryValueExW(key, L"InstallLocation", NULL, &type, (BYTE*)buf, &size) == ERROR_SUCCESS && type == REG_SZ)
        result = buf;
    RegCloseKey(key);
    return result;
}

static DWORD FindAppPid()
{
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE) return 0;
    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(pe);
    DWORD pid = 0;
    if (Process32FirstW(snap, &pe)) {
        do {
            if (_wcsicmp(pe.szExeFile, APP_EXE) == 0) { pid = pe.th32ProcessID; break; }
        } while (Process32NextW(snap, &pe));
    }
    CloseHandle(snap);
    return pid;
}

static DWORD g_closePid = 0;
static BOOL CALLBACK CloseWindowProc(HWND h, LPARAM)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(h, &pid);
    if (pid == g_closePid) PostMessageW(h, WM_CLOSE, 0, 0);
    return TRUE;
}

static bool StopApp()
{
    DWORD pid = FindAppPid();
    if (!pid) return true;
    g_closePid = pid;
    EnumWindows(CloseWindowProc, 0);
    for (int i = 0; i < 25; i++) {
        if (!FindAppPid()) return true;
        Sleep(100);
    }
    HANDLE p = OpenProcess(PROCESS_TERMINATE | SYNCHRONIZE, FALSE, pid);
    if (p) {
        TerminateProcess(p, 0);
        WaitForSingleObject(p, 3000);
        CloseHandle(p);
    }
    return FindAppPid() == 0;
}

static bool Install(const wstring& dir, bool desktop, bool shortcuts, bool reg, bool launch,
    wstring& msg, vector<wstring>& warnings)
{
    wstring err;
    SetProgress(8); SetStatus(L"正在创建安装目录…");
    if (!CreateDir(dir, err)) { msg = err; return false; }
    SetProgress(20); SetStatus(L"正在释放主程序…");
    if (!ExtractResource(IDR_APP, Join(dir, APP_EXE), err)) { msg = err; return false; }
    SetProgress(34); SetStatus(L"正在释放卸载程序…");
    if (!ExtractResource(IDR_UNINSTALL, Join(dir, UNINSTALL_EXE), err)) { msg = err; return false; }
    SetProgress(42); SetStatus(L"正在释放说明文件…");
    ExtractResource(IDR_README, Join(dir, L"使用说明.txt"), err);
    ExtractResource(IDR_README_MD, Join(dir, L"README.md"), err);
    ExtractResource(IDR_PREVIEW, Join(dir, L"preview.png"), err);
    ExtractResource(IDR_SHA, Join(dir, L"SHA256.txt"), err);

    if (shortcuts) {
        SetProgress(60); SetStatus(L"正在创建开始菜单快捷方式…");
        CreateDir(StartMenuDir(), err);
        if (!CreateShortcut(StartMenuLink(), Join(dir, APP_EXE), dir, Join(dir, APP_EXE), APP_NAME))
            warnings.push_back(L"开始菜单快捷方式创建失败。");
        if (desktop) {
            if (!CreateShortcut(DesktopLink(), Join(dir, APP_EXE), dir, Join(dir, APP_EXE), APP_NAME))
                warnings.push_back(L"桌面快捷方式创建失败。");
        }
    }
    if (reg) {
        SetProgress(78); SetStatus(L"正在写入卸载信息…");
        if (!WriteUninstallEntry(dir, err)) warnings.push_back(err);
    }
    SetProgress(94); SetStatus(L"正在完成…");
    if (launch) {
        ShellExecuteW(NULL, L"open", Join(dir, APP_EXE).c_str(), NULL, dir.c_str(), SW_SHOWNORMAL);
    }
    SetProgress(100); SetStatus(L"安装完成");
    return true;
}

static void DeleteDirRecursive(const wstring& dir)
{
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(Join(dir, L"*").c_str(), &fd);
    if (h != INVALID_HANDLE_VALUE) {
        do {
            if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
            wstring p = Join(dir, fd.cFileName);
            if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) DeleteDirRecursive(p);
            else DeleteFileW(p.c_str());
        } while (FindNextFileW(h, &fd));
        FindClose(h);
    }
    RemoveDirectoryW(dir.c_str());
}

static void ScheduleSelfDelete(const wstring& dir, const wstring& uninstaller)
{
    wchar_t sys[MAX_PATH];
    GetSystemDirectoryW(sys, MAX_PATH);
    wstring cmd = wstring(sys) + L"\\cmd.exe";
    wstring args = L"/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + uninstaller + L"\" & rmdir /s /q \"" + dir + L"\"";
    vector<wchar_t> buf(args.begin(), args.end());
    buf.push_back(0);
    wchar_t temp[MAX_PATH];
    GetTempPathW(MAX_PATH, temp);
    STARTUPINFOW si;
    PROCESS_INFORMATION pi;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    ZeroMemory(&pi, sizeof(pi));
    CreateProcessW(cmd.c_str(), &buf[0], NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, temp, &si, &pi);
    if (pi.hProcess) CloseHandle(pi.hProcess);
    if (pi.hThread) CloseHandle(pi.hThread);
}

static bool Uninstall(const wstring& inputDir, bool deleteSettings, bool reg, bool selfDelete,
    wstring& msg, vector<wstring>& warnings)
{
    wstring dir = inputDir;
    if (dir.empty()) dir = ReadInstallLocation();
    if (dir.empty()) dir = ExeDir();
    wstring err;
    try { dir = NormalizeDir(dir, err); }
    catch (...) { msg = L"安装目录无效。"; return false; }
    if (!DirExists(dir)) { msg = L"没有找到安装目录。"; return false; }
    if (FindAppPid()) StopApp();

    SetProgress(25); SetStatus(L"正在删除快捷方式…");
    DeleteShortcuts();
    if (reg) {
        LONG r = RegDeleteKeyW(HKEY_CURRENT_USER, UNINSTALL_KEY);
        if (r != ERROR_SUCCESS && r != ERROR_FILE_NOT_FOUND)
            warnings.push_back(L"删除卸载信息失败（错误 " + std::to_wstring(r) + L"）。");
    }
    if (deleteSettings) DeleteDirRecursive(ConfigDir());

    SetProgress(60); SetStatus(L"正在删除程序文件…");
    wstring uninstaller = Join(dir, UNINSTALL_EXE);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(Join(dir, L"*").c_str(), &fd);
    if (h != INVALID_HANDLE_VALUE) {
        do {
            if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0) continue;
            wstring p = Join(dir, fd.cFileName);
            if (_wcsicmp(p.c_str(), uninstaller.c_str()) == 0) continue;
            if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) DeleteDirRecursive(p);
            else DeleteFileW(p.c_str());
        } while (FindNextFileW(h, &fd));
        FindClose(h);
    }
    SetProgress(90); SetStatus(L"正在完成…");
    if (selfDelete) ScheduleSelfDelete(dir, uninstaller);
    SetProgress(100); SetStatus(L"卸载完成");
    return true;
}

static HFONT MakeFont(int pt, bool bold)
{
    HDC dc = GetDC(NULL);
    int h = -MulDiv(pt, GetDeviceCaps(dc, LOGPIXELSY), 72);
    ReleaseDC(NULL, dc);
    return CreateFontW(h, 0, 0, 0, bold ? FW_BOLD : FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH, L"Microsoft YaHei UI");
}

static void DrawHeader(HDC dc, const RECT& client, const wchar_t* title, const wchar_t* sub, bool red)
{
    HBRUSH bg = CreateSolidBrush(C_HEADER);
    RECT hr = { 0, 0, client.right, 108 };
    FillRect(dc, &hr, bg);
    DeleteObject(bg);
    HBRUSH ob = CreateSolidBrush(red ? C_RED : C_ORANGE);
    HPEN op = CreatePen(PS_SOLID, 1, red ? C_RED : C_ORANGE);
    HGDIOBJ oldB = SelectObject(dc, ob);
    HGDIOBJ oldP = SelectObject(dc, op);
    RoundRect(dc, 26, 28, 76, 78, 14, 14);
    SelectObject(dc, oldB);
    SelectObject(dc, oldP);
    DeleteObject(ob);
    DeleteObject(op);
    POINT tri[3] = { { 51, 38 }, { 64, 68 }, { 38, 68 } };
    HBRUSH wb = CreateSolidBrush(RGB(255, 255, 255));
    HGDIOBJ old = SelectObject(dc, wb);
    Polygon(dc, tri, 3);
    SelectObject(dc, old);
    DeleteObject(wb);

    SetBkMode(dc, TRANSPARENT);
    SetTextColor(dc, RGB(255, 255, 255));
    HGDIOBJ of = SelectObject(dc, g_fontTitle);
    RECT tr = { 92, 26, client.right - 26, 58 };
    DrawTextW(dc, title, -1, &tr, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
    SelectObject(dc, of);
    SetTextColor(dc, RGB(154, 167, 183));
    of = SelectObject(dc, g_fontSmall);
    RECT sr = { 94, 60, client.right - 26, 84 };
    DrawTextW(dc, sub, -1, &sr, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
    SelectObject(dc, of);
    HPEN line = CreatePen(PS_SOLID, 1, RGB(36, 50, 67));
    oldP = SelectObject(dc, line);
    MoveToEx(dc, 26, 100, NULL);
    LineTo(dc, client.right - 26, 100);
    SelectObject(dc, oldP);
    DeleteObject(line);
}

static void DrawInfoCard(HDC dc, const RECT& r)
{
    HBRUSH b = CreateSolidBrush(C_CARD);
    HPEN p = CreatePen(PS_SOLID, 1, C_BORDER);
    HGDIOBJ ob = SelectObject(dc, b);
    HGDIOBJ op = SelectObject(dc, p);
    RoundRect(dc, r.left, r.top, r.right, r.bottom, 14, 14);
    SelectObject(dc, ob);
    SelectObject(dc, op);
    DeleteObject(b);
    DeleteObject(p);
    SetBkMode(dc, TRANSPARENT);
    SetTextColor(dc, C_TEXT);
    HGDIOBJ of = SelectObject(dc, g_fontBold);
    RECT t = { r.left + 18, r.top + 12, r.right - 18, r.top + 34 };
    DrawTextW(dc, L"安装说明", -1, &t, DT_LEFT | DT_SINGLELINE | DT_VCENTER);
    SelectObject(dc, of);
    SetTextColor(dc, C_MUTED);
    of = SelectObject(dc, g_fontSmall);
    RECT l1 = { r.left + 18, r.top + 42, r.right - 18, r.top + 62 };
    DrawTextW(dc, L"• 安装到当前用户目录，不需要管理员权限；程序需要时再单独申请。", -1, &l1, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
    RECT l2 = { r.left + 18, r.top + 68, r.right - 18, r.top + 88 };
    DrawTextW(dc, L"• 只写入程序文件、快捷方式和当前用户的卸载信息。", -1, &l2, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
    RECT l3 = { r.left + 18, r.top + 94, r.right - 18, r.top + 114 };
    DrawTextW(dc, L"• 不修改游戏文件、ACE 文件、驱动或服务；设置备份在 %LocalAppData%\\DeltaForceTuner。", -1, &l3, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
    SelectObject(dc, of);
}

static LRESULT DrawOwnerdrawButton(const DRAWITEMSTRUCT* dis, bool red)
{
    COLORREF fill = red ? C_RED : C_ORANGE;
    if (dis->itemState & ODS_DISABLED) fill = RGB(190, 197, 205);
    else if (dis->itemState & ODS_SELECTED) fill = red ? RGB(184, 42, 42) : C_ORANGE_DARK;
    HBRUSH b = CreateSolidBrush(fill);
    HPEN p = CreatePen(PS_SOLID, 1, fill);
    HGDIOBJ ob = SelectObject(dis->hDC, b);
    HGDIOBJ op = SelectObject(dis->hDC, p);
    RoundRect(dis->hDC, dis->rcItem.left, dis->rcItem.top, dis->rcItem.right, dis->rcItem.bottom, 10, 10);
    SelectObject(dis->hDC, ob);
    SelectObject(dis->hDC, op);
    DeleteObject(b);
    DeleteObject(p);
    wchar_t text[128] = { 0 };
    GetWindowTextW(dis->hwndItem, text, 127);
    SetBkMode(dis->hDC, TRANSPARENT);
    SetTextColor(dis->hDC, RGB(255, 255, 255));
    HGDIOBJ of = SelectObject(dis->hDC, g_fontBold);
    RECT tr = dis->rcItem;
    DrawTextW(dis->hDC, text, -1, &tr, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
    SelectObject(dis->hDC, of);
    return TRUE;
}

static void SetBusy(bool busy)
{
    g_busy = busy;
    EnableWindow(g_edit, !busy);
    EnableWindow(g_browse, !busy);
    EnableWindow(g_desktop, !busy);
    EnableWindow(g_launch, !busy);
    EnableWindow(g_install, !busy);
    EnableWindow(g_cancel, !busy);
}

static void DoInstall(HWND hwnd)
{
    wstring err;
    wstring dir = NormalizeDir(GetText(g_edit), err);
    if (dir.empty()) {
        MessageBoxW(hwnd, err.c_str(), L"安装目录", MB_OK | MB_ICONWARNING);
        return;
    }
    if (FindAppPid()) {
        if (MessageBoxW(hwnd, L"检测到程序正在运行。是否关闭后继续安装？", L"程序正在运行",
            MB_YESNO | MB_ICONQUESTION) != IDYES) return;
        StopApp();
    }
    SetBusy(true);
    SetProgress(3);
    SetStatus(L"正在准备…");
    vector<wstring> warnings;
    wstring msg;
    bool ok = Install(dir, SendMessageW(g_desktop, BM_GETCHECK, 0, 0) == BST_CHECKED,
        true, true, false, msg, warnings);
    if (!ok) {
        SetBusy(false);
        MessageBoxW(hwnd, (L"安装失败：\n" + msg).c_str(), L"安装失败", MB_OK | MB_ICONERROR);
        SetStatus(L"安装失败");
        return;
    }
    if (SendMessageW(g_launch, BM_GETCHECK, 0, 0) == BST_CHECKED)
        ShellExecuteW(NULL, L"open", Join(dir, APP_EXE).c_str(), NULL, dir.c_str(), SW_SHOWNORMAL);
    wstring text = L"安装完成。\n\n安装目录：\n" + dir;
    if (!warnings.empty()) {
        text += L"\n\n注意：\n";
        for (size_t i = 0; i < warnings.size(); i++) text += L"• " + warnings[i] + L"\n";
    }
    MessageBoxW(hwnd, text.c_str(), L"安装完成", MB_OK | MB_ICONINFORMATION);
    PostQuitMessage(0);
}

static LRESULT CALLBACK SetupProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg) {
    case WM_CREATE:
    {
        g_edit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", DefaultDir().c_str(),
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL, 26, 146, 520, 26, hwnd, (HMENU)IDC_EDIT_DIR, g_inst, NULL);
        g_browse = CreateWindowExW(0, L"BUTTON", L"浏览…", WS_CHILD | WS_VISIBLE | WS_TABSTOP,
            556, 144, 138, 30, hwnd, (HMENU)IDC_BROWSE, g_inst, NULL);
        g_desktop = CreateWindowExW(0, L"BUTTON", L"创建桌面快捷方式",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_AUTOCHECKBOX, 26, 190, 300, 24, hwnd, (HMENU)IDC_DESKTOP, g_inst, NULL);
        g_launch = CreateWindowExW(0, L"BUTTON", L"安装完成后启动程序",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_AUTOCHECKBOX, 26, 216, 300, 24, hwnd, (HMENU)IDC_LAUNCH, g_inst, NULL);
        g_progress = CreateWindowExW(0, PROGRESS_CLASSW, NULL, WS_CHILD | WS_VISIBLE,
            26, 420, 668, 14, hwnd, (HMENU)IDC_PROGRESS, g_inst, NULL);
        g_install = CreateWindowExW(0, L"BUTTON", L"安装", WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_OWNERDRAW,
            520, 454, 174, 42, hwnd, (HMENU)IDC_INSTALL, g_inst, NULL);
        g_cancel = CreateWindowExW(0, L"BUTTON", L"取消", WS_CHILD | WS_VISIBLE | WS_TABSTOP,
            406, 454, 104, 42, hwnd, (HMENU)IDC_CANCEL, g_inst, NULL);
        SendMessageW(g_edit, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessageW(g_browse, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessageW(g_desktop, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessageW(g_launch, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessageW(g_install, WM_SETFONT, (WPARAM)g_fontBold, TRUE);
        SendMessageW(g_cancel, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessageW(g_desktop, BM_SETCHECK, BST_CHECKED, 0);
        SendMessageW(g_launch, BM_SETCHECK, BST_CHECKED, 0);
        SendMessageW(g_progress, PBM_SETRANGE, 0, MAKELPARAM(0, 100));
        SendMessageW(g_progress, PBM_SETBARCOLOR, 0, C_ORANGE);
        SendMessageW(g_progress, PBM_SETBKCOLOR, 0, C_CARD);
        g_hwnd = hwnd;
        g_status = L"准备就绪";
        SetFocus(g_install);
        return 0;
    }
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC dc = BeginPaint(hwnd, &ps);
        RECT rc;
        GetClientRect(hwnd, &rc);
        HBRUSH bg = CreateSolidBrush(C_BG);
        FillRect(dc, &rc, bg);
        DeleteObject(bg);
        DrawHeader(dc, rc, APP_NAME, L"安装程序 v1.0.1 · 本地运行 · 不修改游戏文件", false);

        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, C_TEXT);
        HGDIOBJ of = SelectObject(dc, g_fontBold);
        RECT lr = { 26, 122, 300, 144 };
        DrawTextW(dc, L"安装位置", -1, &lr, DT_LEFT | DT_SINGLELINE | DT_VCENTER);
        SelectObject(dc, of);

        DrawInfoCard(dc, RECT{ 26, 254, 694, 382 });

        SetTextColor(dc, C_MUTED);
        of = SelectObject(dc, g_fontSmall);
        RECT sr = { 26, 390, 694, 416 };
        DrawTextW(dc, g_status.c_str(), -1, &sr, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
        RECT fr = { 26, 500, 694, 520 };
        DrawTextW(dc, L"安装器只写入程序文件、快捷方式和当前用户卸载信息；所有设置都可在程序内恢复。", -1, &fr, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
        SelectObject(dc, of);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_TIMER:
        if (wp == 1) {
            DestroyWindow(hwnd);
            return 0;
        }
        break;
    case WM_CTLCOLOREDIT:
    {
        HDC dc = (HDC)wp;
        SetTextColor(dc, C_TEXT);
        SetBkColor(dc, RGB(255, 255, 255));
        return (LRESULT)GetStockObject(WHITE_BRUSH);
    }
    case WM_CTLCOLORBTN:
    {
        HDC dc = (HDC)wp;
        SetTextColor(dc, C_TEXT);
        SetBkColor(dc, C_BG);
        return (LRESULT)GetStockObject(WHITE_BRUSH);
    }
    case WM_DRAWITEM:
        return DrawOwnerdrawButton((const DRAWITEMSTRUCT*)lp, false);
    case WM_COMMAND:
        switch (LOWORD(wp)) {
        case IDC_BROWSE:
        {
            BROWSEINFOW bi;
            ZeroMemory(&bi, sizeof(bi));
            bi.hwndOwner = hwnd;
            bi.lpszTitle = L"选择安装文件夹";
            bi.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
            LPITEMIDLIST idl = SHBrowseForFolderW(&bi);
            if (idl) {
                wchar_t path[MAX_PATH];
                if (SHGetPathFromIDListW(idl, path)) SetWindowTextW(g_edit, path);
                CoTaskMemFree(idl);
            }
            return 0;
        }
        case IDC_INSTALL:
            if (!g_busy) DoInstall(hwnd);
            return 0;
        case IDC_CANCEL:
            if (!g_busy) DestroyWindow(hwnd);
            return 0;
        }
        return 0;
    case WM_CLOSE:
        if (!g_busy) DestroyWindow(hwnd);
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

static int RunSetup()
{
    WNDCLASSEXW wc;
    ZeroMemory(&wc, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = SetupProc;
    wc.hInstance = g_inst;
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hIcon = LoadIconW(g_inst, MAKEINTRESOURCEW(IDI_APP));
    wc.hIconSm = wc.hIcon;
    wc.lpszClassName = L"DFTSetupWindow";
    RegisterClassExW(&wc);

    RECT wr = { 0, 0, 720, 526 };
    AdjustWindowRectEx(&wr, WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, FALSE, 0);
    HWND hwnd = CreateWindowExW(0, wc.lpszClassName, L"安装 · 三角洲行动 本机调优助手",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT, CW_USEDEFAULT, wr.right - wr.left, wr.bottom - wr.top,
        NULL, NULL, g_inst, NULL);
    if (!hwnd) return 1;
    ShowWindow(hwnd, SW_SHOW);
    UpdateWindow(hwnd);
    if (g_uiTest) SetTimer(hwnd, 1, 700, NULL);
    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return 0;
}

static void DoUninstall(HWND hwnd)
{
    if (MessageBoxW(hwnd, L"确定要卸载吗？\n\n将删除程序文件和快捷方式；游戏文件不会被删除。",
        L"确认卸载", MB_YESNO | MB_ICONWARNING) != IDYES) return;
    if (FindAppPid()) {
        if (MessageBoxW(hwnd, L"程序正在运行。是否关闭后继续卸载？", L"程序正在运行",
            MB_YESNO | MB_ICONQUESTION) != IDYES) return;
        StopApp();
    }
    g_busy = true;
    EnableWindow(g_uninstall, FALSE);
    EnableWindow(g_uCancel, FALSE);
    EnableWindow(g_delSettings, FALSE);
    vector<wstring> warnings;
    wstring msg;
    bool ok = Uninstall(g_uninstallDir, SendMessageW(g_delSettings, BM_GETCHECK, 0, 0) == BST_CHECKED,
        true, true, msg, warnings);
    wstring text = ok ? L"卸载完成。" : (L"卸载失败：\n" + msg);
    if (!warnings.empty()) {
        text += L"\n\n注意：\n";
        for (size_t i = 0; i < warnings.size(); i++) text += L"• " + warnings[i] + L"\n";
    }
    MessageBoxW(hwnd, text.c_str(), ok ? L"卸载完成" : L"卸载失败", MB_OK | (ok ? MB_ICONINFORMATION : MB_ICONERROR));
    PostQuitMessage(0);
}

static LRESULT CALLBACK UninstallProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg) {
    case WM_CREATE:
    {
        g_delSettings = CreateWindowExW(0, L"BUTTON", L"同时删除设置备份（%LocalAppData%\\DeltaForceTuner）",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_AUTOCHECKBOX, 26, 206, 520, 24, hwnd, (HMENU)IDC_DELETE_SETTINGS, g_inst, NULL);
        g_uninstall = CreateWindowExW(0, L"BUTTON", L"卸载", WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_OWNERDRAW,
            402, 286, 172, 42, hwnd, (HMENU)IDC_UNINSTALL, g_inst, NULL);
        g_uCancel = CreateWindowExW(0, L"BUTTON", L"取消", WS_CHILD | WS_VISIBLE | WS_TABSTOP,
            288, 286, 104, 42, hwnd, (HMENU)IDC_CANCEL, g_inst, NULL);
        SendMessageW(g_delSettings, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessageW(g_uninstall, WM_SETFONT, (WPARAM)g_fontBold, TRUE);
        SendMessageW(g_uCancel, WM_SETFONT, (WPARAM)g_font, TRUE);
        g_hwnd = hwnd;
        g_uninstallDir = ReadInstallLocation();
        if (g_uninstallDir.empty()) g_uninstallDir = ExeDir();
        g_status = L"准备卸载";
        return 0;
    }
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC dc = BeginPaint(hwnd, &ps);
        RECT rc;
        GetClientRect(hwnd, &rc);
        HBRUSH bg = CreateSolidBrush(C_BG);
        FillRect(dc, &rc, bg);
        DeleteObject(bg);
        DrawHeader(dc, rc, L"卸载 三角洲行动 · 本机调优助手",
            L"不会删除游戏文件、ACE 文件或 Windows 安全设置", true);
        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, C_TEXT);
        HGDIOBJ of = SelectObject(dc, g_fontBold);
        RECT lr = { 26, 120, 580, 142 };
        DrawTextW(dc, L"安装目录", -1, &lr, DT_LEFT | DT_SINGLELINE | DT_VCENTER);
        SelectObject(dc, of);
        SetTextColor(dc, C_MUTED);
        of = SelectObject(dc, g_fontSmall);
        RECT pr = { 26, 144, 570, 170 };
        DrawTextW(dc, g_uninstallDir.c_str(), -1, &pr, DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
        RECT nr = { 26, 176, 570, 198 };
        DrawTextW(dc, L"卸载会删除程序文件、快捷方式和当前用户的卸载信息。", -1, &nr, DT_LEFT | DT_SINGLELINE | DT_VCENTER);
        RECT wr = { 44, 234, 560, 260 };
        DrawTextW(dc, L"默认保留设置备份，方便以后重新安装；游戏文件不会被删除。", -1, &wr, DT_LEFT | DT_SINGLELINE | DT_VCENTER);
        RECT sr = { 26, 334, 570, 354 };
        DrawTextW(dc, g_status.c_str(), -1, &sr, DT_LEFT | DT_SINGLELINE | DT_VCENTER);
        SelectObject(dc, of);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_CTLCOLORBTN:
    {
        HDC dc = (HDC)wp;
        SetTextColor(dc, C_TEXT);
        SetBkColor(dc, C_BG);
        return (LRESULT)GetStockObject(WHITE_BRUSH);
    }
    case WM_DRAWITEM:
        return DrawOwnerdrawButton((const DRAWITEMSTRUCT*)lp, true);
    case WM_COMMAND:
        switch (LOWORD(wp)) {
        case IDC_UNINSTALL:
            if (!g_busy) DoUninstall(hwnd);
            return 0;
        case IDC_CANCEL:
            if (!g_busy) DestroyWindow(hwnd);
            return 0;
        }
        return 0;
    case WM_CLOSE:
        if (!g_busy) DestroyWindow(hwnd);
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

static int RunUninstall()
{
    WNDCLASSEXW wc;
    ZeroMemory(&wc, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = UninstallProc;
    wc.hInstance = g_inst;
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hIcon = LoadIconW(g_inst, MAKEINTRESOURCEW(IDI_APP));
    wc.hIconSm = wc.hIcon;
    wc.lpszClassName = L"DFTUninstallWindow";
    RegisterClassExW(&wc);
    RECT wr = { 0, 0, 600, 360 };
    AdjustWindowRectEx(&wr, WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, FALSE, 0);
    HWND hwnd = CreateWindowExW(0, wc.lpszClassName, L"卸载 · 三角洲行动 本机调优助手",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT, CW_USEDEFAULT, wr.right - wr.left, wr.bottom - wr.top,
        NULL, NULL, g_inst, NULL);
    if (!hwnd) return 1;
    ShowWindow(hwnd, SW_SHOW);
    UpdateWindow(hwnd);
    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return 0;
}

static vector<wstring> ParseArgs()
{
    vector<wstring> out;
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv) return out;
    for (int i = 1; i < argc; i++) out.push_back(argv[i]);
    LocalFree(argv);
    return out;
}

static bool HasArg(const vector<wstring>& args, const wchar_t* name)
{
    for (size_t i = 0; i < args.size(); i++) if (_wcsicmp(args[i].c_str(), name) == 0) return true;
    return false;
}

static wstring GetArgValue(const vector<wstring>& args, const wchar_t* name)
{
    wstring prefix = wstring(name) + L"=";
    for (size_t i = 0; i < args.size(); i++) {
        if (_wcsnicmp(args[i].c_str(), prefix.c_str(), prefix.size()) == 0)
            return args[i].substr(prefix.size());
    }
    return wstring();
}

static int SilentInstall(const vector<wstring>& args)
{
    wstring dir = GetArgValue(args, L"--dir");
    if (dir.empty()) dir = DefaultDir();
    wstring err;
    dir = NormalizeDir(dir, err);
    if (dir.empty()) { AppendLog(L"FAIL " + err); return 1; }
    bool shortcuts = !HasArg(args, L"--no-shortcuts");
    bool desktop = !HasArg(args, L"--no-desktop");
    bool reg = !HasArg(args, L"--no-registry");
    bool launch = !HasArg(args, L"--no-launch");
    if (FindAppPid()) StopApp();
    vector<wstring> warnings;
    wstring msg;
    bool ok = Install(dir, desktop, shortcuts, reg, launch, msg, warnings);
    AppendLog((ok ? L"OK " : L"FAIL ") + dir);
    for (size_t i = 0; i < warnings.size(); i++) AppendLog(L"WARN " + warnings[i]);
    return ok ? 0 : 1;
}

static int SilentUninstall(const vector<wstring>& args)
{
    wstring dir = GetArgValue(args, L"--dir");
    bool delSettings = HasArg(args, L"--delete-settings");
    bool reg = !HasArg(args, L"--no-registry");
    bool selfDelete = !HasArg(args, L"--no-self-delete");
    vector<wstring> warnings;
    wstring msg;
    bool ok = Uninstall(dir, delSettings, reg, selfDelete, msg, warnings);
    AppendLog((ok ? L"OK uninstall " : L"FAIL uninstall ") + msg);
    for (size_t i = 0; i < warnings.size(); i++) AppendLog(L"WARN " + warnings[i]);
    return ok ? 0 : 1;
}

int WINAPI wWinMain(HINSTANCE inst, HINSTANCE, PWSTR, int)
{
    g_inst = inst;
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    INITCOMMONCONTROLSEX icc;
    icc.dwSize = sizeof(icc);
    icc.dwICC = ICC_STANDARD_CLASSES | ICC_PROGRESS_CLASS;
    InitCommonControlsEx(&icc);
    g_font = MakeFont(9, false);
    g_fontBold = MakeFont(9, true);
    g_fontTitle = MakeFont(18, true);
    g_fontSmall = MakeFont(8, false);

    vector<wstring> args = ParseArgs();
#ifdef UNINSTALL
    if (HasArg(args, L"--quiet")) return SilentUninstall(args);
    return RunUninstall();
#else
    if (HasArg(args, L"--install")) return SilentInstall(args);
    g_uiTest = HasArg(args, L"--ui-test");
    return RunSetup();
#endif
}
