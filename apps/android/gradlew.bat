@ECHO OFF
SETLOCAL
SET APP_HOME=%~dp0

java.exe -classpath "%APP_HOME%gradle\wrapper\gradle-wrapper.jar" org.gradle.wrapper.GradleWrapperMain %*
IF %ERRORLEVEL% EQU 0 GOTO end

EXIT /B %ERRORLEVEL%

:end
ENDLOCAL

