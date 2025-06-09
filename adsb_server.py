import asyncio
import websockets
import json
import signal # Standard library for signal handling
import logging

# --- Configuration ---
EXTERNAL_WS_URI = "ws://192.87.172.71:1338"
LOCAL_WS_HOST = "0.0.0.0" # this is very dependant on if you are 'at home vs' on site 
LOCAL_WS_PORT = 9000        # Port Unity will connect to IMPORTANTY alex ! 

# --- Logging Setup ---
logging.basicConfig(
    level=logging.INFO, 
    format='%(asctime)s - %(levelname)s - [%(name)s:%(lineno)d] - %(message)s' 
)
logger = logging.getLogger(__name__) # Logger for this specific module
ws_logger = logging.getLogger('websockets') # Get the logger for the websockets library
ws_logger.setLevel(logging.WARNING) # Set websockets library logging to WARNING to reduce noise

# --- Store connected clients (Unity instances) ---
CONNECTED_CLIENTS = set()
# --- Global shutdown event ---
shutdown_event = asyncio.Event()


async def register_client(websocket_client, path: str):
    """Adds a new client and handles its lifecycle."""
    if shutdown_event.is_set():
        logger.warning(f"Shutdown in progress. Rejecting new client: {websocket_client.remote_address}")
        try:
            await websocket_client.close(code=1012, reason="Server shutting down") 
        except websockets.exceptions.ConnectionClosed:
            pass # closed
        return

    CONNECTED_CLIENTS.add(websocket_client)
    logger.info(f"Client connected: {websocket_client.remote_address} (Path: '{path}'). Total clients: {len(CONNECTED_CLIENTS)}")
    client_wait_task = asyncio.create_task(websocket_client.wait_closed(), name=f"ClientWait_{websocket_client.remote_address}")
    shutdown_listen_task = asyncio.create_task(shutdown_event.wait(), name=f"ClientShutdownListen_{websocket_client.remote_address}")

    try:
        done, pending = await asyncio.wait(
            [client_wait_task, shutdown_listen_task],
            return_when=asyncio.FIRST_COMPLETED,
        )

        if shutdown_listen_task in done: # Server initiated shutdown for this client
            logger.info(f"Server shutting down. Closing client: {websocket_client.remote_address}")
            if not websocket_client.closed:
                await websocket_client.close(code=1012, reason="Server shutting down")
        # client closed connection or an error occurred on it

        # Cancel any pending task from this pair
        for task in pending:
            task.cancel()
            try:
                await task # llow cancellation to propagate
            except asyncio.CancelledError:
                pass

    except websockets.exceptions.ConnectionClosedOK: # might be caught by wait_closed() itself
        logger.info(f"Client connection closed OK by client: {websocket_client.remote_address}")
    except websockets.exceptions.ConnectionClosedError as e:
        logger.warning(f"Client connection closed with error: {websocket_client.remote_address} - {e}")
    except Exception as e:
        logger.error(f"Unexpected error in client handler {websocket_client.remote_address}: {e}", exc_info=True)
    finally:
        if websocket_client in CONNECTED_CLIENTS:
            CONNECTED_CLIENTS.remove(websocket_client)
        logger.info(f"Client session ended: {websocket_client.remote_address}. Total clients: {len(CONNECTED_CLIENTS)}")


async def broadcast_message(message_str: str):
    """Sends a message to all connected local clients."""
    if not message_str:
        logger.debug("Broadcast attempt with empty message. Skipping.")
        return

    if CONNECTED_CLIENTS:
        logger.debug(f"Broadcasting to {len(CONNECTED_CLIENTS)} clients: {message_str[:100]}...")
        clients_to_send = list(CONNECTED_CLIENTS)
        tasks = []
        for client in clients_to_send:
            if client.open:
                tasks.append(asyncio.create_task(client.send(message_str), name=f"SendTo_{client.remote_address}"))
            else:
                logger.warning(f"Client {client.remote_address} was closed. Skipping send.")

        if tasks:
            results = await asyncio.gather(*tasks, return_exceptions=True)
            for i, result in enumerate(results): #  tasks and results maintain order
                if isinstance(result, Exception):
                    # the original task to get its name for better logging if needed
                    original_task = tasks[i]
                    logger.error(f"Error sending (Task: {original_task.get_name()}): {result}")
    else:
        logger.debug("No clients connected to broadcast to.")


async def receive_from_external_adsb():
    """Connects to the external ADS-B source and forwards messages. Stops when shutdown_event is set."""
    logger.info("External ADS-B receiver task started.")
    while not shutdown_event.is_set():
        try:
            logger.info(f"Attempting to connect to external ADS-B: {EXTERNAL_WS_URI}")
            async with websockets.connect(EXTERNAL_WS_URI) as external_websocket:
                logger.info(f"Successfully connected to external ADS-B: {EXTERNAL_WS_URI}")
                while not shutdown_event.is_set():
                    try:
                        message_str = await asyncio.wait_for(external_websocket.recv(), timeout=1.0)
                        if message_str:
                            await broadcast_message(message_str)
                    except asyncio.TimeoutError:
                        continue
                    except websockets.exceptions.ConnectionClosed:
                        logger.warning("External ADS-B connection closed during receive. Reconnecting...")
                        break
                    except Exception as e:
                        logger.error(f"Error receiving from external ADS-B: {e}. Reconnecting...", exc_info=True)
                        break
        except (websockets.exceptions.WebSocketException, ConnectionRefusedError, OSError) as e:
            logger.warning(f"External ADS-B connection issue ({type(e).__name__}): {e}. Retrying in 5s...")
        except Exception as e:
            logger.error(f"Unexpected error in external ADS-B connection task: {e}", exc_info=True)
        
        if not shutdown_event.is_set():
            try:
                await asyncio.wait_for(shutdown_event.wait(), timeout=5.0)
            except asyncio.TimeoutError:
                pass
    logger.info("External ADS-B receiver task stopped.")


async def main_server_logic():
    """Main asynchronous logic to run the server and data forwarder."""
    try:
        server = await websockets.serve(
            register_client,
            LOCAL_WS_HOST,
            LOCAL_WS_PORT,
        )
    except OSError as e:
        if e.errno == 10048:
            logger.critical(f"CRITICAL: Port {LOCAL_WS_PORT} already in use. Close other app or change port.")
            return
        else:
            logger.critical(f"CRITICAL: OSError during server startup: {e}", exc_info=True)
            return
    except Exception as e:
        logger.critical(f"CRITICAL: Failed to start local server: {e}", exc_info=True)
        return

    logger.info(f"Local WebSocket server started on ws://{LOCAL_WS_HOST}:{LOCAL_WS_PORT}")

    external_data_task = asyncio.create_task(receive_from_external_adsb(), name="ExternalDataReceiver")
    shutdown_wait_task = asyncio.create_task(shutdown_event.wait(), name="ShutdownEventWatcher")

    logger.info("Main server logic running. Waiting for tasks or shutdown signal...")
    done, pending = await asyncio.wait(
        [shutdown_wait_task, external_data_task],
        return_when=asyncio.FIRST_COMPLETED
    )

    if external_data_task in done:
        logger.warning("External data task finished or failed.")
        if external_data_task.exception():
            logger.error(f"External data task exited with exception: {external_data_task.exception()}", exc_info=external_data_task.exception())
    elif shutdown_wait_task in done:
        logger.info("Shutdown event was triggered.")
    else: # Should not happen with FIRST_COMPLETED if tasks are in the list.. I HOPE  
        logger.warning("Asyncio.wait returned unexpectedly.")


    logger.info("Initiating shutdown sequence...")
    if not shutdown_event.is_set():
        shutdown_event.set()

    # Cancel tasks that might still be pending from the main wait waiting more... 
    for task in pending:
        if not task.done():
            task.cancel()

    logger.info("Closing local server to new connections...")
    server.close()
    try:
        await asyncio.wait_for(server.wait_closed(), timeout=5.0)
        logger.info("Local server new connections closed.")
    except asyncio.TimeoutError:
        logger.warning("Timeout waiting for server to close new connections.")


    # Wait for the primary tasks to finish their shutdown
    tasks_to_await = []
    if not external_data_task.done():
        tasks_to_await.append(external_data_task)
    if not shutdown_wait_task.done(): # Though it should be doneif it triggered shutdown
         tasks_to_await.append(shutdown_wait_task)


    if tasks_to_await:
        logger.info(f"Waiting for main tasks to complete: {[t.get_name() for t in tasks_to_await]}")
        try:
            await asyncio.wait(tasks_to_await, timeout=10.0)
        except asyncio.TimeoutError:
            logger.warning("Timeout waiting for main tasks to complete during shutdown.")
            for task in tasks_to_await:
                if not task.done():
                    logger.warning(f"Task {task.get_name()} did not finish, cancelling.")
                    task.cancel()
                    try:
                        await task # Allow cancellation
                    except asyncio.CancelledError:
                        logger.info(f"Task {task.get_name()} cancelled.")
                    except Exception as e_task_cancel:
                        logger.error(f"Error awaiting cancelled task {task.get_name()}: {e_task_cancel}")

    logger.info("Server shutdown process complete.")


def os_signal_handler(sig, frame):
    """Handle OS signals like SIGINT (Ctrl+C) and SIGTERM."""
    logger.info(f"Received OS signal {signal.Signals(sig).name}. Initiating graceful shutdown...")
    # This function is called in the main thread, not an asyncio event loop thread.
    # Setting the event is thread-safe.
    if not shutdown_event.is_set():
        shutdown_event.set()
    else:
        logger.warning("Shutdown already in progress.")


if __name__ == "__main__":
    # Setup signal handlers for graceful shutdown
    signal.signal(signal.SIGINT, os_signal_handler)  # Ctrl+C

    # Start the asyncio event loop and run the server
    asyncio.run(main_server_logic())

    # end of custom script eddited from UT files 