using System;
using System.Collections.Concurrent;

namespace Ventriloquist
{
	public class TTSProducer
	{
		private ConcurrentQueue<TTSRequest> queue;

		public TTSProducer ()
		{
			queue = Ventriloquist.Queue;
		}

		public void QueueRequest(TTSRequest request)
		{
			queue.Enqueue (request);
		}
	}
}

