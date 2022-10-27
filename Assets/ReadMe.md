# Async Serializer

Expected output:
```xml
<?xml version="1.0" encoding="utf-16"?>
<ArrayOfAsyncSerializer.SaveValue xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://schemas.datacontract.org/2004/07/">
	<AsyncSerializer.SaveValue>
		<Key>Container</Key>
		<Value i:type=":Container.SaveValues">
			<alpha>1</alpha>
			<beta>1</beta>
			<gamma>1</gamma>
			<list xmlns:d4p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays">
				<d4p1:string>0</d4p1:string>
			</list>
			<nested>
				<NestedData>
					<list xmlns:d6p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays">
						<d6p1:string>0</d6p1:string>
					</list>
				</NestedData>
			</nested>
		</Value>
	</AsyncSerializer.SaveValue>
```