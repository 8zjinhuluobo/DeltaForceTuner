#define UNICODE
#define _UNICODE
#include <windows.h>
int WINAPI wWinMain(HINSTANCE h, HINSTANCE p, PWSTR cmd, int show) {
    MessageBoxW(NULL, L"C++ 安装器编译测试", L"OK", MB_OK);
    return 0;
}
