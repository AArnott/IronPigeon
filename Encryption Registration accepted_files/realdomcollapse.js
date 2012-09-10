
window.onload=function()
{
	domcollapse(); 	
	//alert('I am here');
	//document.getElementById(focusItem).focus; 
	//var loadLocation = 'contactPersonFirst';
	//saveScrollCoordinates(loadLocation);  
	//  enables domcollapse all three links and allows several expanded elements
	// domcollapse('makeunique'); 	
	//  enables domcollapse and allows only one expanded element
	// domcollapse('nounique'); 
	//  enables domcollapse offers no make one element unique link
}
function nothing(nameIn){
	focusItem = nameIn;
}
function isEmpty(strng) {
var error = "";
  if (strng.length == 0) {
     error = "The mandatory text area has not been filled in.\n"
  }
return error;	  
}

/*
	DomCollapse 
	Turns elements of a page into triggers to collapse and expand other elements
	written by Christian Heilmann
	For all enquiries check the homepage: http://www.onlinetools.org/tools/domcollapse/
*/
function domcollapse(cmd)
{
	if(!document.getElementById || !document.createTextNode){return;}
/* Variables */
	// Class Names
	var normalTriggerClass='trigger';
	var expandedTriggerClass='triggerexpanded';
	var hoverTriggerClass='triggerhover';
	var normalElementClass='elementnormal';
	var collapsedElementClass='elementcollapsed'
	// Images
	// the %section% will be replaced by the text content of the trigger element
	var expandMessage='Expand section: %section%';
	var expandImage='./docs/images/plus.gif';
	var collapseMessage='Collapse section: %section%';
	var collapseImage='./docs/images/minus.gif';
	// Messages
	var collapseAllMessage='collapse all';
	var expandAllMessage='expand all';
	var triggerUniqueMessage='Only allow one expanded element';
	var triggerNonUniqueMessage='Allow multiple expanded elements';
	// element to add collapse/expand all links to
	var collapseAllElement='domcollapseall';
	// id that triggers the 'show only one element' functionality
	// this gets set automatically
	var enableAlternateElement='domcollapsealternate';

	var triggers=document.getElementsByTagName('*'); // Change as applicable!

	switch(cmd)
	{
/* collapse all */
		case 0:
			for(var i=0;i<triggers.length;i++)
			{
				if(!cssjs('check',triggers[i],normalTriggerClass) && !cssjs('check',triggers[i],expandedTriggerClass)){continue;}
				cssjs('remove',triggers[i],expandedTriggerClass);
				cssjs('add',triggers[i],normalTriggerClass);
				cssjs('remove',triggers[i].hideElement,normalElementClass)
				cssjs('add',triggers[i].hideElement,collapsedElementClass)
				addimage(triggers[i],false);
			}
		break;
/* expand all */
		case 1:
			for(var i=0;i<triggers.length;i++)
			{
				if(!cssjs('check',triggers[i],normalTriggerClass) && !cssjs('check',triggers[i],expandedTriggerClass)){continue;}
				cssjs('remove',triggers[i],normalTriggerClass);
				cssjs('add',triggers[i],expandedTriggerClass);
				cssjs('remove',triggers[i].hideElement,collapsedElementClass)
				cssjs('add',triggers[i].hideElement,normalElementClass)
				addimage(triggers[i],true);
			}
		break;
		default:
/* initialise all */
			for(var i=0;i<triggers.length;i++)
			{
				if(!cssjs('check',triggers[i],normalTriggerClass) && !cssjs('check',triggers[i],expandedTriggerClass)){continue;}
				var newa=document.createElement('a');
				var newimg=document.createElement('img');
				var locexpandMessage=expandMessage.replace(/%section%/,triggers[i].firstChild.nodeValue);
				var loccollapseMessage=collapseMessage.replace(/%section%/,triggers[i].firstChild.nodeValue);
				newimg.src=cssjs('check',triggers[i],expandedTriggerClass)?collapseImage:expandImage;
				newimg.alt=cssjs('check',triggers[i],expandedTriggerClass)?loccollapseMessage:locexpandMessage;
				newimg.title=cssjs('check',triggers[i],expandedTriggerClass)?loccollapseMessage:locexpandMessage;;
				newa.appendChild(newimg);
				newa.href='#';
				triggers[i].insertBefore(newa,triggers[i].firstChild);
				var tohide=triggers[i].nextSibling;
				while(tohide.nodeType!=1)
				{
					tohide=tohide.nextSibling;
				}
				var toadd=cssjs('check',triggers[i],expandedTriggerClass)?normalElementClass:collapsedElementClass;
				cssjs('add',tohide,toadd);
				triggers[i].hideElement=tohide;
				triggers[i].onmouseover=function()
				{
					cssjs('add',this,hoverTriggerClass);
				}
				triggers[i].onmouseout=function()
				{
					cssjs('remove',this,hoverTriggerClass);
				}
				triggers[i].onclick=function()
				{
// collapse all before showing the current element
					if(document.getElementById(enableAlternateElement))
					{
						for(var i=0;i<triggers.length;i++)
						{
							if(!cssjs('check',triggers[i],normalTriggerClass) && !cssjs('check',triggers[i],expandedTriggerClass)){continue;}
							if(triggers[i]==this){continue;}
							cssjs('remove',triggers[i],expandedTriggerClass)
							cssjs('add',triggers[i],normalTriggerClass)
							cssjs('remove',triggers[i].hideElement,normalElementClass)
							cssjs('add',triggers[i].hideElement,collapsedElementClass)
							addimage(triggers[i],false);
						}
						if(cssjs('check',this,expandedTriggerClass))
						{
							cssjs('swap',this,expandedTriggerClass,normalTriggerClass)
							cssjs('swap',this.hideElement,normalElementClass,collapsedElementClass)
							addimage(this,false);
						} else {
							cssjs('swap',this,normalTriggerClass,expandedTriggerClass)
							cssjs('swap',this.hideElement,collapsedElementClass,normalElementClass)
							addimage(this,true);
						}	
					} else {
// show hide on click of the trigger element
						if(cssjs('check',this,expandedTriggerClass))
						{
							cssjs('swap',this,expandedTriggerClass,normalTriggerClass)
							cssjs('swap',this.hideElement,normalElementClass,collapsedElementClass)
							addimage(this,false);
						} else {
							cssjs('swap',this,normalTriggerClass,expandedTriggerClass)
							cssjs('swap',this.hideElement,collapsedElementClass,normalElementClass)
							addimage(this,true);
						}	
					}
					return false;
				}
			}
		break;
	}
/* Collapse and Expand all links */
	var metalinks=document.getElementById(collapseAllElement);
	if(!metalinks || metalinks.getElementsByTagName('ul')[0]){return;}
	var newul=document.createElement('ul');
	var newli=document.createElement('li');
	newa=document.createElement('a');
	newa.href='#';
	newa.onclick=function(){domcollapse(1);return false;}
	newa.appendChild(document.createTextNode(expandAllMessage));
	newli.appendChild(newa);
	newul.appendChild(newli);		

	newli=document.createElement('li');
	newa=document.createElement('a');
	newa.href='#';
	newa.onclick=function(){domcollapse(0);return false;}
	newa.appendChild(document.createTextNode(collapseAllMessage));
	newli.appendChild(newa);
	newul.appendChild(newli);
	if(cmd!='nounique')
	{
		newli=document.createElement('li');
		newa=document.createElement('a');
		newa.href='#';
		newa.onclick=function()
		{
			if(this.id==enableAlternateElement)
			{
				this.removeAttribute('id');
				this.replaceChild(document.createTextNode(triggerUniqueMessage),this.firstChild);					
			} else {
				this.id=enableAlternateElement;
				this.replaceChild(document.createTextNode(triggerNonUniqueMessage),this.firstChild);					
			}
			return false;
		}
		if(cmd=='makeunique')
		{
			newa.id=enableAlternateElement;
			newa.appendChild(document.createTextNode(triggerNonUniqueMessage));
		} else {
			newa.appendChild(document.createTextNode(triggerUniqueMessage));
		}
		newli.appendChild(newa);
		newul.appendChild(newli);
	}
	metalinks.appendChild(newul);			

	function addimage (o,state)
	{
		var locexpandMessage=expandMessage.replace(/%section%/,o.childNodes[1].nodeValue);
		var loccollapseMessage=collapseMessage.replace(/%section%/,o.childNodes[1].nodeValue);
		o.getElementsByTagName('img')[0].src=state?collapseImage:expandImage;
		o.getElementsByTagName('img')[0].alt=state?loccollapseMessage:locexpandMessage;
		o.getElementsByTagName('img')[0].title=state?loccollapseMessage:locexpandMessage;
	}
	function cssjs(a,o,c1,c2)
	{
		switch (a){
			case 'swap':
				o.className=!cssjs('check',o,c1)?o.className.replace(c2,c1):o.className.replace(c1,c2);
			break;
			case 'add':
				if(!cssjs('check',o,c1)){o.className+=o.className?' '+c1:c1;}
			break;
			case 'remove':
				var rep=o.className.match(' '+c1)?' '+c1:c1;
				o.className=o.className.replace(rep,'');
			break;
			case 'check':
				return new RegExp('\\b'+c1+'\\b').test(o.className)
			break;
		}
	}
}

