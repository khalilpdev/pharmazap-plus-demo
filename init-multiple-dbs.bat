@echo off
setlocal enabledelayedexpansion

if "%POSTGRES_MULTIPLE_DATABASES%"=="" (
  echo Variável POSTGRES_MULTIPLE_DATABASES não definida.
  exit /b 1
)

if "%POSTGRES_USER%"=="" (
  echo Variável POSTGRES_USER não definida.
  exit /b 1
)

for %%D in (%POSTGRES_MULTIPLE_DATABASES:,= %) do (
  echo Criando banco de dados: %%D
  psql -v ON_ERROR_STOP=1 --username "%POSTGRES_USER%" -d postgres -c "CREATE DATABASE \"%%D\";"
  if errorlevel 1 exit /b !errorlevel!
  psql -v ON_ERROR_STOP=1 --username "%POSTGRES_USER%" -d postgres -c "GRANT ALL PRIVILEGES ON DATABASE \"%%D\" TO \"%POSTGRES_USER%\";"
  if errorlevel 1 exit /b !errorlevel!
)

endlocal
