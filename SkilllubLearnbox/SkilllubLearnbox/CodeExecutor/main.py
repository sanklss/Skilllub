from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import subprocess
import time
import sys
import io

app = FastAPI(title="Code Executor")

class CodeRequest(BaseModel):
    code: str
    language: str
    stdin: str = ""
    timeout: int = 5

class CodeResponse(BaseModel):
    output: str
    error: str
    execution_time: float
    success: bool

@app.post("/execute")
async def execute_code(request: CodeRequest):
    start = time.time()
    
    try:
        if request.language == "python":
            process = subprocess.Popen(
                ["python", "-c", request.code],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                timeout=request.timeout
            )
            
            try:
                stdout, stderr = process.communicate(input=request.stdin, timeout=request.timeout)
                
                return CodeResponse(
                    output=stdout,
                    error=stderr,
                    execution_time=(time.time() - start) * 1000,
                    success=process.returncode == 0
                )
            except subprocess.TimeoutExpired:
                process.kill()
                stdout, stderr = process.communicate()
                return CodeResponse(
                    output=stdout,
                    error="Timeout error: " + stderr,
                    execution_time=request.timeout * 1000,
                    success=False
                )
            
        elif request.language == "javascript" or request.language == "js":
            process = subprocess.Popen(
                ["node", "-e", request.code],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                timeout=request.timeout
            )
            
            try:
                stdout, stderr = process.communicate(input=request.stdin, timeout=request.timeout)
                
                return CodeResponse(
                    output=stdout,
                    error=stderr,
                    execution_time=(time.time() - start) * 1000,
                    success=process.returncode == 0
                )
            except subprocess.TimeoutExpired:
                process.kill()
                stdout, stderr = process.communicate()
                return CodeResponse(
                    output=stdout,
                    error="Timeout error: " + stderr,
                    execution_time=request.timeout * 1000,
                    success=False
                )
            
        else:
            raise HTTPException(400, f"Язык {request.language} не поддерживается")
            
    except Exception as e:
        return CodeResponse(
            output="",
            error=str(e),
            execution_time=(time.time() - start) * 1000,
            success=False
        )

@app.get("/health")
async def health():
    return {
        "status": "healthy",
        "languages": ["python", "javascript"]
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000, workers=4)