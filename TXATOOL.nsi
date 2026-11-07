; TXATOOL - WIN installer (NSIS 3, Modern UI 2)
; Build: compile_setup.bat  (publishes win-x64 self-contained, then runs makensis)

Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!define APP_NAME      "TXATOOL - WIN"
!define APP_EXE       "TXATOOL.exe"
!define APP_VERSION   "1.0.0"
!define APP_PUBLISHER "TXA"
!define APP_URL       "https://fb.com/vlog.txa.2311"
!define UNINST_KEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\TXATOOL-WIN"
!define PUBLISH_DIR   "bin\Release\net9.0-windows\win-x64\publish"

Var PREV_VERSION
Var PREV_DIR
Var PREV_UNINSTALL

Name "${APP_NAME}"
OutFile "Output\TXATOOL_Setup_v${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${UNINST_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP_NAME} ${APP_VERSION}"

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 ${APP_PUBLISHER}"

!define MUI_ICON   "Images\icon.ico"
!define MUI_UNICON "Images\icon.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"
!define MUI_LANGDLL_ALLLANGUAGES

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Vietnamese"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "${APP_NAME} requires 64-bit Windows 10 or later."
    Abort
  ${EndIf}
  SetRegView 64

  ; 1. Kill running instances of TXATOOL so files are not locked
  nsExec::Exec 'taskkill /F /T /IM ${APP_EXE}'
  Pop $0
  Sleep 500

  ; 2. Check if a previous version of TXATOOL is already installed
  ReadRegStr $PREV_VERSION HKLM "${UNINST_KEY}" "DisplayVersion"
  ReadRegStr $PREV_DIR HKLM "${UNINST_KEY}" "InstallLocation"
  ReadRegStr $PREV_UNINSTALL HKLM "${UNINST_KEY}" "UninstallString"

  ${If} $PREV_VERSION == ""
    ReadRegStr $PREV_VERSION HKCU "${UNINST_KEY}" "DisplayVersion"
    ReadRegStr $PREV_DIR HKCU "${UNINST_KEY}" "InstallLocation"
    ReadRegStr $PREV_UNINSTALL HKCU "${UNINST_KEY}" "UninstallString"
  ${EndIf}

  ; Bỏ qua thông báo xác nhận nếu đang chạy ở chế độ im lặng (/S từ Auto-Update)
  IfSilent init_done

  ${If} $PREV_VERSION != ""
    ${If} $PREV_VERSION == "${APP_VERSION}"
      MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON1 "Phiên bản ${APP_VERSION} hiện đã được cài đặt trên máy của bạn.$\n$\nBạn có muốn cài đè (ghi đè toàn bộ file) phiên bản này không?" IDYES init_done
      Abort
    ${Else}
      MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON1 "Phát hiện ${APP_NAME} (phiên bản cũ $PREV_VERSION) đã được cài đặt trên máy.$\n$\nBộ cài sẽ tự động gỡ bỏ phiên bản cũ trước khi nâng cấp lên phiên bản mới ${APP_VERSION}.$\n$\nBạn có muốn tiếp tục cài đặt không?" IDYES init_done
      Abort
    ${EndIf}
  ${EndIf}
init_done:
FunctionEnd

Section "Install"
  SetRegView 64
  SetShellVarContext all

  ; Double check: kill running app again before writing files
  DetailPrint "Đang kiểm tra và đóng ứng dụng nếu đang chạy..."
  nsExec::Exec 'taskkill /F /T /IM ${APP_EXE}'
  Pop $0
  Sleep 500

  SetOverwrite on

  ; Nếu phát hiện phiên bản KHÁC/CŨ HƠN thì mới chạy gỡ cài đặt bản cũ trước
  ; Nếu là CÙNG PHIÊN BẢN (cài đè) thì ghi đè trực tiếp mà không cần chạy uninstaller
  ${If} $PREV_VERSION != ""
  ${AndIf} $PREV_VERSION != "${APP_VERSION}"
    ${If} $PREV_DIR != ""
    ${AndIf} ${FileExists} "$PREV_DIR\Uninstall.exe"
      DetailPrint "Đang tự động gỡ bỏ phiên bản cũ tại: $PREV_DIR..."
      ExecWait '"$PREV_DIR\Uninstall.exe" /S _?=$PREV_DIR' $0
      Sleep 1000
      Delete "$PREV_DIR\Uninstall.exe"
      RMDir "$PREV_DIR"
    ${ElseIf} ${FileExists} "$INSTDIR\Uninstall.exe"
      DetailPrint "Đang tự động gỡ bỏ phiên bản cũ tại: $INSTDIR..."
      ExecWait '"$INSTDIR\Uninstall.exe" /S _?=$INSTDIR' $0
      Sleep 1000
      Delete "$INSTDIR\Uninstall.exe"
      RMDir "$INSTDIR"
    ${ElseIf} $PREV_UNINSTALL != ""
      DetailPrint "Đang chạy trình gỡ cài đặt của phiên bản cũ..."
      ExecWait '$PREV_UNINSTALL /S _?=$INSTDIR' $0
      Sleep 1000
    ${EndIf}
  ${ElseIf} $PREV_VERSION == "${APP_VERSION}"
    DetailPrint "Cùng phiên bản ${APP_VERSION} - Đang tiến hành cài đè các tệp tin..."
  ${EndIf}

  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "URLInfoAbout" "${APP_URL}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  SetRegView 64
  SetShellVarContext all
  DetailPrint "Đang đóng ứng dụng nếu đang chạy..."
  nsExec::Exec 'taskkill /F /T /IM ${APP_EXE}'
  Pop $0
  Sleep 500

  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$DESKTOP\TXATOOL.lnk"
  RMDir /r "$SMPROGRAMS\${APP_NAME}"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegKey HKCU "${UNINST_KEY}"
SectionEnd
