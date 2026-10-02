FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py search.py chat.py prompt.json search-prompt.json chat-prompt.json ./

EXPOSE 8080

# The three prompt files are bind-mounted in voice-stack.yml, so editing any of them
# on the host and restarting the container applies the change without rebuilding this image.
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8080"]
