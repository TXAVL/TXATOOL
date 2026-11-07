; TXATOOL - WIN installer (NSIS 3, Modern UI 2)
; Multilingual Edition: Tiếng Việt & English
; Build: compile_setup.bat

Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!define APP_NAME      "TXATOOL - WIN"
!define APP_EXE       "TXATOOL.exe"
!define APP_VERSION   "1.1.0"
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
BrandingText "${APP_NAME} ${APP_VERSION} • TXA Official"

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Multilingual Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 ${APP_PUBLISHER}"

; ===================================================================
; Modern UI 2 Configuration & Visual Styling
; ===================================================================
!define MUI_ICON   "Images\icon.ico"
!define MUI_UNICON "Images\icon.ico"
!define MUI_ABORTWARNING

; Modern UI Header & Sidebar Bitmaps
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "Images\installer_header.bmp"
!define MUI_HEADERIMAGE_RIGHT
!define MUI_WELCOMEFINISHPAGE_BITMAP "Images\installer_sidebar.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "Images\installer_sidebar.bmp"

; Language selection dialog settings
!define MUI_LANGDLL_REGISTRY_ROOT "HKLM"
!define MUI_LANGDLL_REGISTRY_KEY "${UNINST_KEY}"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"
!define MUI_LANGDLL_ALLLANGUAGES

; --- Installer Pages ---
!define MUI_WELCOMEPAGE_TITLE $(WELCOME_TITLE)
!define MUI_WELCOMEPAGE_TEXT $(WELCOME_TEXT)
!insertmacro MUI_PAGE_WELCOME

!define MUI_DIRECTORYPAGE_TEXT_TOP $(DIR_TEXT_TOP)
!define MUI_DIRECTORYPAGE_TEXT_DESTINATION $(DIR_TEXT_DEST)
!insertmacro MUI_PAGE_DIRECTORY

!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_TITLE $(FINISH_TITLE)
!define MUI_FINISHPAGE_TEXT $(FINISH_TEXT)
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT $(FINISH_RUN_TEXT)
!define MUI_FINISHPAGE_LINK $(FINISH_LINK_TEXT)
!define MUI_FINISHPAGE_LINK_LOCATION "https://txavl.github.io/txa-repo/docs/"
!insertmacro MUI_PAGE_FINISH

; --- Uninstaller Pages ---
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; ===================================================================
; Multi-Language Configuration
; ===================================================================
!insertmacro MUI_LANGUAGE "Vietnamese"
!insertmacro MUI_LANGUAGE "English"

; ------------------- TIẾNG VIỆT -------------------
LangString MSG_REQ_64BIT ${LANG_VIETNAMESE} "${APP_NAME} yêu cầu hệ điều hành Windows 10 hoặc Windows 11 (64-bit)."
LangString MSG_SAME_VER ${LANG_VIETNAMESE} "Phiên bản ${APP_VERSION} hiện đã được cài đặt trên máy tính của bạn.$\n$\nBạn có muốn cài đè (ghi đè toàn bộ tệp tin) phiên bản này không?"
LangString MSG_PREV_VER ${LANG_VIETNAMESE} "Phát hiện ${APP_NAME} (phiên bản cũ $PREV_VERSION) đã được cài đặt trên máy.$\n$\nBộ cài sẽ tự động gỡ bỏ phiên bản cũ trước khi nâng cấp lên phiên bản mới ${APP_VERSION}.$\n$\nBạn có muốn tiếp tục cài đặt không?"

LangString WELCOME_TITLE ${LANG_VIETNAMESE} "Chào mừng bạn đến với Bộ cài đặt ${APP_NAME}"
LangString WELCOME_TEXT ${LANG_VIETNAMESE} "Trình hướng dẫn này sẽ hỗ trợ bạn cài đặt ${APP_NAME} ${APP_VERSION} vào máy tính.$\n$\n${APP_NAME} là giải pháp all-in-one chuyên nghiệp dành cho kỹ thuật viên IT:$\n• Cài Windows trực tiếp từ ISO (WSAP - Không cần USB Boot)$\n• Bộ công cụ xử lý lỗi chia sẻ máy in mạng LAN chuyên sâu$\n• Chuyển đổi ấn bản Windows (Convert SKU) & Kích hoạt bản quyền số$\n• Tinh chỉnh tối ưu hóa hiệu năng, dọn rác & gỡ Bloatware UWP$\n• Kho phần mềm tuyển chọn & Cài đặt Microsoft Office tùy biến$\n$\nNhấn 'Tiếp tục' để bắt đầu thiết lập."

LangString DIR_TEXT_TOP ${LANG_VIETNAMESE} "Thiết lập sẽ cài đặt ${APP_NAME} vào thư mục sau. Nhấn 'Duyệt' nếu bạn muốn cài vào thư mục khác."
LangString DIR_TEXT_DEST ${LANG_VIETNAMESE} "Thư mục cài đặt đích"

LangString DETAIL_KILL_APP ${LANG_VIETNAMESE} "Đang kiểm tra và đóng ứng dụng nếu đang chạy..."
LangString DETAIL_REMOVE_OLD ${LANG_VIETNAMESE} "Đang tự động gỡ bỏ phiên bản cũ..."
LangString DETAIL_OVERWRITE ${LANG_VIETNAMESE} "Cùng phiên bản ${APP_VERSION} - Đang tiến hành cài đè các tệp tin..."

LangString FINISH_TITLE ${LANG_VIETNAMESE} "Hoàn tất Cài đặt ${APP_NAME}"
LangString FINISH_TEXT ${LANG_VIETNAMESE} "${APP_NAME} ${APP_VERSION} đã được cài đặt thành công trên máy tính của bạn.$\n$\nNhấn 'Hoàn tất' để đóng trình hướng dẫn cài đặt."
LangString FINISH_RUN_TEXT ${LANG_VIETNAMESE} "Khởi chạy ${APP_NAME} ngay"
LangString FINISH_LINK_TEXT ${LANG_VIETNAMESE} "Truy cập Tài liệu hướng dẫn sử dụng TXA (Docs)"
LangString SHORTCUT_DESC ${LANG_VIETNAMESE} "TXATOOL - WIN: Phần mềm tối ưu hóa & cứu hộ Windows - Phát triển bởi TXA"
LangString SHORTCUT_UNINST_DESC ${LANG_VIETNAMESE} "Gỡ cài đặt ${APP_NAME}"

LangString UNINST_CLOSING ${LANG_VIETNAMESE} "Đang đóng ứng dụng nếu đang chạy trước khi gỡ bỏ..."

; ------------------- ENGLISH -------------------
LangString MSG_REQ_64BIT ${LANG_ENGLISH} "${APP_NAME} requires 64-bit Windows 10 or Windows 11."
LangString MSG_SAME_VER ${LANG_ENGLISH} "Version ${APP_VERSION} is already installed on your computer.$\n$\nDo you want to reinstall and overwrite all existing files?"
LangString MSG_PREV_VER ${LANG_ENGLISH} "A previous version ($PREV_VERSION) of ${APP_NAME} was detected on this computer.$\n$\nThe installer will automatically remove the previous version before upgrading to ${APP_VERSION}.$\n$\nDo you want to continue?"

LangString WELCOME_TITLE ${LANG_ENGLISH} "Welcome to the ${APP_NAME} Setup Wizard"
LangString WELCOME_TEXT ${LANG_ENGLISH} "This wizard will guide you through the installation of ${APP_NAME} ${APP_VERSION}.$\n$\n${APP_NAME} is an all-in-one suite designed for IT technicians and power users:$\n• Direct Windows ISO Deployment (WSAP - No USB Boot needed)$\n• In-depth Network LAN Printer Repair Toolkit$\n• Windows SKU Conversion & Digital Licensing Manager$\n• Deep OS Performance Optimization & UWP Bloatware Cleaner$\n• Curated App Store & Customizable Microsoft Office Deployment$\n$\nClick 'Next' to continue."

LangString DIR_TEXT_TOP ${LANG_ENGLISH} "Setup will install ${APP_NAME} in the following folder. Click 'Browse' to choose a different location."
LangString DIR_TEXT_DEST ${LANG_ENGLISH} "Destination Folder"

LangString DETAIL_KILL_APP ${LANG_ENGLISH} "Checking and terminating running instances of ${APP_NAME}..."
LangString DETAIL_REMOVE_OLD ${LANG_ENGLISH} "Automatically removing previous version..."
LangString DETAIL_OVERWRITE ${LANG_ENGLISH} "Same version ${APP_VERSION} detected - Overwriting application files..."

LangString FINISH_TITLE ${LANG_ENGLISH} "Completing ${APP_NAME} Setup"
LangString FINISH_TEXT ${LANG_ENGLISH} "${APP_NAME} ${APP_VERSION} has been successfully installed on your computer.$\n$\nClick 'Finish' to exit this wizard."
LangString FINISH_RUN_TEXT ${LANG_ENGLISH} "Launch ${APP_NAME} now"
LangString FINISH_LINK_TEXT ${LANG_ENGLISH} "Visit TXA Documentation (Docs)"
LangString SHORTCUT_DESC ${LANG_ENGLISH} "TXATOOL - WIN: Windows Optimization & Rescue Toolkit - Developed by TXA"
LangString SHORTCUT_UNINST_DESC ${LANG_ENGLISH} "Uninstall ${APP_NAME}"

LangString UNINST_CLOSING ${LANG_ENGLISH} "Terminating running instances before uninstalling..."

; ===================================================================
; Init & Installation Logic
; ===================================================================
Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "$(MSG_REQ_64BIT)"
    Abort
  ${EndIf}
  SetRegView 64

  ; 1. Kill running instances of TXATOOL so files are not locked
  nsExec::Exec 'taskkill /F /T /IM ${APP_EXE}'
  Pop $0
  Sleep 500

  ; 2. Language selection dialog (skip if running silent e.g. /S)
  ${IfNot} ${Silent}
    !insertmacro MUI_LANGDLL_DISPLAY
  ${EndIf}

  ; 3. Check if a previous version of TXATOOL is already installed
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
      MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON1 "$(MSG_SAME_VER)" IDYES init_done
      Abort
    ${Else}
      MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON1 "$(MSG_PREV_VER)" IDYES init_done
      Abort
    ${EndIf}
  ${EndIf}
init_done:
FunctionEnd

Function un.onInit
  SetRegView 64
  !insertmacro MUI_UNGETLANGUAGE
FunctionEnd

Section "Install"
  SetRegView 64
  SetShellVarContext all

  DetailPrint "$(DETAIL_KILL_APP)"
  nsExec::Exec 'taskkill /F /T /IM ${APP_EXE}'
  Pop $0
  Sleep 500

  SetOverwrite on

  ; Nếu phát hiện phiên bản KHÁC/CŨ HƠN thì mới chạy gỡ cài đặt bản cũ trước
  ; Nếu là CÙNG PHIÊN BẢN (cài đè) thì ghi đè trực tiếp mà không cần chạy uninstaller
  ${If} $PREV_VERSION != ""
  ${AndIf} $PREV_VERSION != "${APP_VERSION}"
    DetailPrint "$(DETAIL_REMOVE_OLD)"
    ${If} $PREV_DIR != ""
    ${AndIf} ${FileExists} "$PREV_DIR\Uninstall.exe"
      ExecWait '"$PREV_DIR\Uninstall.exe" /S _?=$PREV_DIR' $0
      Sleep 1000
      Delete "$PREV_DIR\Uninstall.exe"
      RMDir "$PREV_DIR"
    ${ElseIf} ${FileExists} "$INSTDIR\Uninstall.exe"
      ExecWait '"$INSTDIR\Uninstall.exe" /S _?=$INSTDIR' $0
      Sleep 1000
      Delete "$INSTDIR\Uninstall.exe"
      RMDir "$INSTDIR"
    ${ElseIf} $PREV_UNINSTALL != ""
      ExecWait '$PREV_UNINSTALL /S _?=$INSTDIR' $0
      Sleep 1000
    ${EndIf}
  ${ElseIf} $PREV_VERSION == "${APP_VERSION}"
    DetailPrint "$(DETAIL_OVERWRITE)"
  ${EndIf}

  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 "" "" "$(SHORTCUT_DESC)"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0 "" "" "$(SHORTCUT_UNINST_DESC)"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0 "" "" "$(SHORTCUT_DESC)"

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
  DetailPrint "$(UNINST_CLOSING)"
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
