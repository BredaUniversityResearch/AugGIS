FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py checks.py conversation.py voice-prompt.json ./

EXPOSE 8080

# voice-prompt.json is bind-mounted in voice-stack.yml, so editing it on the host and
# restarting the container applies the change without rebuilding this image.
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8080"]
